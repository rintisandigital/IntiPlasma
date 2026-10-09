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
public sealed class MobileRecordingTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x02, 0x03];

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record RoleResponse(Guid Id, string Name);

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

    private sealed record Setup(
        Guid Branch,
        string PplA,
        string Manager,
        Guid CycleA,
        Guid CycleB,
        Guid WarehouseA,
        Guid WarehouseB,
        Guid Central,
        Guid Feed,
        Guid Sak);

    /// <summary>
    /// A branch with PPL A (coop A), PPL B (coop B) and a Manager; both coops run a cycle of 1,000 birds that
    /// started three days ago and hold 1,000 kg of feed; the branch also has a central warehouse with feed.
    /// </summary>
    private async Task<Setup> SetupAsync()
    {
        await AuthenticateAsAdminAsync();
        string suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        Guid branch = await PostAsync<Guid>("branches", new { code = $"R{suffix}", name = "Cabang Recording" });
        Guid profile = await PostAsync<Guid>("branch-access-profiles",
            new { name = $"Rec {suffix}", allBranches = false, branchIds = new[] { branch } });
        List<RoleResponse>? roles = await HttpClient.GetFromJsonAsync<List<RoleResponse>>("roles");
        Guid pplRole = roles!.Single(r => r.Name == MobileRoles.FieldOfficer).Id;
        Guid managerRole = roles!.Single(r => r.Name == MobileRoles.Manager).Id;

        (Guid pplAId, string pplA) = await CreateUserAsync(pplRole, profile, branch);
        (Guid pplBId, _) = await CreateUserAsync(pplRole, profile, branch);
        (_, string manager) = await CreateUserAsync(managerRole, profile, branch);

        await AuthenticateAsAdminAsync();
        Guid farmer = await PostAsync<Guid>("farmers", new
        {
            code = $"RF{suffix}",
            name = "Peternak Recording",
            type = "Inti",
            branchId = branch,
            taxIdentity = new { isPkp = false },
            bankAccount = new { },
            fieldOfficerUserId = (Guid?)null
        });
        Guid coopA = await PostAsync<Guid>("coops", Coop(farmer, $"RA{suffix}", pplAId));
        Guid coopB = await PostAsync<Guid>("coops", Coop(farmer, $"RB{suffix}", pplBId));

        Guid warehouseA = await CoopWarehouseAsync(coopA);
        Guid warehouseB = await CoopWarehouseAsync(coopB);

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Guid kg = await db.Uoms.Where(u => u.Code == "KG").Select(u => u.Id).SingleAsync();
        Guid sak = await db.Uoms.Where(u => u.Code == "SAK").Select(u => u.Id).SingleAsync();
        var feed = Item.Create($"RP{suffix}", "Pakan Starter", ItemCategory.Feed, kg, null);
        feed.SetConversions([(sak, 50m)]).IsSuccess.ShouldBeTrue();
        db.Items.Add(feed);

        var central = Warehouse.CreateCentral($"RG{suffix}", "Gudang Induk", branch, null);
        db.Warehouses.Add(central);

        foreach (Guid warehouseId in new[] { warehouseA, warehouseB, central.Id })
        {
            var balance = StockBalance.Open(warehouseId, feed.Id);
            var movement = new StockMovement(Today, StockMovementType.Receipt, "Test", Guid.NewGuid(), $"TEST-{suffix}", null);
            db.StockLedgerEntries.Add(balance.Receive(movement, 1_000m, new Money(8_000_000m)).Value);
            db.StockBalances.Add(balance);
        }

        Farmer farmerEntity = await db.Farmers.SingleAsync(f => f.Id == farmer);
        Guid cycleA = StartCycle(db, await db.Coops.SingleAsync(c => c.Id == coopA), farmerEntity, $"RCA{suffix}");
        Guid cycleB = StartCycle(db, await db.Coops.SingleAsync(c => c.Id == coopB), farmerEntity, $"RCB{suffix}");

        await db.SaveChangesAsync();

        return new Setup(branch, pplA, manager, cycleA, cycleB, warehouseA, warehouseB, central.Id, feed.Id, sak);
    }

    private static Guid StartCycle(ApplicationDbContext db, Coop coop, Farmer farmer, string number)
    {
        ProductionCycle cycle = ProductionCycle.Plan(number, coop, farmer, null, Today.AddDays(-3), 1_000, null).Value;
        cycle.Start(Today.AddDays(-3), 1_000).IsSuccess.ShouldBeTrue();
        db.ProductionCycles.Add(cycle);

        return cycle.Id;
    }

    /// <summary>
    /// The coop warehouse is created by the outbox after the coop is saved.
    /// </summary>
    private async Task<Guid> CoopWarehouseAsync(Guid coopId)
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            Guid? id = await CountAsync(db => db.Warehouses.Where(w => w.CoopId == coopId).Select(w => (Guid?)w.Id).SingleOrDefaultAsync());
            if (id is { } warehouseId)
            {
                return warehouseId;
            }

            await Task.Delay(500);
        }

        throw new InvalidOperationException($"No warehouse was created for coop {coopId}");
    }

    private async Task<T> CountAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using IServiceScope scope = Services.CreateScope();

        return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
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

    private static object Coop(Guid farmerId, string code, Guid fieldOfficerUserId) => new
    {
        farmerId,
        code,
        name = $"Kandang {code}",
        capacity = 5_000,
        houseType = "ClosedHouse",
        fieldOfficerUserId
    };

    private async Task<(Guid Id, string AccessToken)> CreateUserAsync(Guid roleId, Guid profileId, Guid branchId)
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);

        await AuthenticateAsAdminAsync();
        (await HttpClient.PutAsJsonAsync($"users/{userId}/roles", new { roleIds = new[] { roleId } })).EnsureSuccessStatusCode();
        (await HttpClient.PutAsJsonAsync($"users/{userId}/access", new { branchAccessProfileId = profileId, defaultBranchId = branchId }))
            .EnsureSuccessStatusCode();

        return (userId, (await LoginAsync(email)).AccessToken);
    }

    private async Task<T> PostAsync<T>(string url, object body)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(url, body);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"POST {url} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
