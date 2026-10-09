using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Domain.Inventory.Stock;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.MasterData.Items;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Cycles;
using Domain.Roles;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Production;

/// <summary>
/// PLAN-MOBILE M2: the field context for offline forms, replaying the offline queue (attachments and daily
/// recordings with client ids) and the PPL scope of the stock balances (M-40, M-41).
/// </summary>
public sealed class MobileRecordingTests(IntegrationTestWebAppFactory factory) : FieldScenarioTest(factory)
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x02, 0x03];


    private sealed record FieldContext(DateOnly ServerDate, List<FieldCycle> Cycles, List<FieldItem> Items, List<FieldStock> Stock);

    private sealed record FieldCycle(
        Guid Id,
        Guid CoopId,
        Guid? WarehouseId,
        int InitialPopulation,
        int CurrentPopulation,
        DateOnly? LastRecordingDate,
        List<DateOnly> RecordedDates);

    private sealed record FieldItem(Guid Id, string Code, List<FieldUom> Uoms);

    private sealed record FieldUom(Guid UomId, string Code, decimal Factor);

    private sealed record FieldStock(Guid WarehouseId, Guid ItemId, decimal Quantity);

    private sealed record Attachment(Guid Id);

    private sealed record Balance(Guid WarehouseId);

    private sealed record BalancePage(List<Balance> Items);

    [Fact]
    public async Task FieldContext_Should_ContainOnlyThePplCycles_WithUnitsAndStockQuantity()
    {
        Setup s = await SetupAsync();
        Authenticate(s.PplA);

        HttpResponseMessage response = await HttpClient.GetAsync("mobile/field-context");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string json = await response.Content.ReadAsStringAsync();
        FieldContext context = (await response.Content.ReadFromJsonAsync<FieldContext>())!;

        FieldCycle cycle = context.Cycles.ShouldHaveSingleItem();
        cycle.Id.ShouldBe(s.CycleA);
        cycle.WarehouseId.ShouldBe(s.WarehouseA);
        cycle.InitialPopulation.ShouldBe(1_000);
        cycle.CurrentPopulation.ShouldBe(1_000);
        cycle.LastRecordingDate.ShouldBeNull();

        FieldItem feed = context.Items.Single(i => i.Id == s.Feed);
        feed.Uoms.Select(u => (u.Code, u.Factor)).ShouldBe([("KG", 1m), ("SAK", 50m)]);
        context.Stock.ShouldBe([new FieldStock(s.WarehouseA, s.Feed, 1_000m)]);

        // Values and costs stay on the server.
        json.ShouldNotContain("value", Case.Insensitive);
        json.ShouldNotContain("cost", Case.Insensitive);
    }

    [Fact]
    public async Task OfflineQueue_Should_BeReplayable_WithoutDuplicates()
    {
        Setup s = await SetupAsync();
        Authenticate(s.PplA);
        var attachmentId = Guid.CreateVersion7();
        var recordingId = Guid.CreateVersion7();

        // The app resends an item when the response got lost: same ids → same attachment and recording.
        (await UploadAsync(attachmentId)).Id.ShouldBe(attachmentId);
        (await UploadAsync(attachmentId)).Id.ShouldBe(attachmentId);

        object recording = Recording(recordingId, s.CycleA, Today, s.Feed, s.Sak, attachmentId);
        (await PostRecordingAsync(recording)).StatusCode.ShouldBe(HttpStatusCode.OK);
        HttpResponseMessage replay = await PostRecordingAsync(recording);
        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await replay.Content.ReadFromJsonAsync<Guid>()).ShouldBe(recordingId);

        (await CountAsync(db => db.DailyRecordings.CountAsync(r => r.CycleId == s.CycleA))).ShouldBe(1);
        (await CountAsync(db => db.Attachments.CountAsync(a => a.Id == attachmentId))).ShouldBe(1);

        // The same id for another date is a conflict; another PPL's cycle does not exist for this PPL.
        HttpResponseMessage otherDate = await PostRecordingAsync(Recording(recordingId, s.CycleA, Today.AddDays(-1), s.Feed, s.Sak, null));
        otherDate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await otherDate.Content.ReadAsStringAsync()).ShouldContain("DailyRecordings.IdBelongsToOtherRecording");
        (await PostRecordingAsync(Recording(Guid.CreateVersion7(), s.CycleB, Today, s.Feed, s.Sak, null)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // The field context follows: recorded date, population and stock (2 SAK × 50 kg).
        FieldContext context = (await HttpClient.GetFromJsonAsync<FieldContext>("mobile/field-context"))!;
        FieldCycle cycle = context.Cycles.ShouldHaveSingleItem();
        cycle.RecordedDates.ShouldBe([Today]);
        cycle.LastRecordingDate.ShouldBe(Today);
        cycle.CurrentPopulation.ShouldBe(1_000 - 5 - 1);
        context.Stock.Single(x => x.ItemId == s.Feed).Quantity.ShouldBe(900m);
    }

    [Fact]
    public async Task StockBalances_Should_OnlyShowThePplCoopWarehouses()
    {
        Setup s = await SetupAsync();

        Authenticate(s.PplA);
        BalancePage page = (await HttpClient.GetFromJsonAsync<BalancePage>($"inventory/stock-balances?branchId={s.Branch}&includeEmpty=true"))!;
        page.Items.Select(b => b.WarehouseId).Distinct().ShouldBe([s.WarehouseA]);
        (await HttpClient.GetAsync($"inventory/stock-card?warehouseId={s.WarehouseB}&itemId={s.Feed}&from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        Authenticate(s.Manager);
        page = (await HttpClient.GetFromJsonAsync<BalancePage>($"inventory/stock-balances?branchId={s.Branch}&includeEmpty=true"))!;
        page.Items.Select(b => b.WarehouseId).Distinct().ShouldBe([s.WarehouseA, s.WarehouseB, s.Central], ignoreOrder: true);
    }

    private static object Recording(Guid id, Guid cycleId, DateOnly date, Guid feedId, Guid sakId, Guid? attachmentId) => new
    {
        id,
        cycleId,
        date,
        mortality = 5,
        culling = 1,
        averageBodyWeightGram = 180m,
        notes = "Dari aplikasi",
        usages = new[] { new { itemId = feedId, uomId = sakId, quantity = 2m } },
        documents = attachmentId is { } a ? new[] { a } : Array.Empty<Guid>()
    };

    private async Task<HttpResponseMessage> PostRecordingAsync(object recording)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "production/daily-recordings")
        {
            Content = JsonContent.Create(recording)
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        return await HttpClient.SendAsync(request);
    }

    private async Task<Attachment> UploadAsync(Guid id)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Jpeg);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "recording.jpg");
        using var idField = new StringContent(id.ToString());
        form.Add(idField, "id");

        HttpResponseMessage response = await HttpClient.PostAsync("attachments", form);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<Attachment>())!;
    }
}
