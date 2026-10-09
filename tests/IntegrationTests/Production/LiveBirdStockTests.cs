using System.Net;
using System.Net.Http.Json;
using Domain.MasterData.WeightRanges;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Production;

/// <summary>
/// Stok ayam harian (PLAN-MOBILE M3): upsert per weight range, population limit, PPL scope, Manager read-only,
/// summary from the latest report and deleting recent entries (M-47, M-49 s.d. M-52).
/// </summary>
public sealed class LiveBirdStockTests(IntegrationTestWebAppFactory factory) : FieldScenarioTest(factory)
{
    private sealed record Ranges(Guid Small, Guid Medium, Guid Large);

    private sealed record Entry(Guid Id, Guid WeightRangeId, int Birds, decimal WeightKg, decimal AverageWeightKg);

    private sealed record RangeTotal(Guid WeightRangeId, int Birds, decimal WeightKg, int Coops);

    private sealed record CoopSummary(Guid CycleId, DateOnly? ReportDate, int? DataAgeDays, bool IsStale, int TotalBirds);

    private sealed record Summary(List<RangeTotal> Ranges, List<CoopSummary> Coops, int TotalBirds);

    private sealed record FieldCycle(Guid Id, DateOnly? LatestStockDate, List<Entry> LatestStock);

    private sealed record FieldRange(Guid Id, string Code);

    private sealed record FieldContext(List<FieldCycle> Cycles, List<FieldRange> WeightRanges);

    [Fact]
    public async Task Upsert_Should_ChangeTheEntryOfTheSameRange_InsteadOfAddingOne()
    {
        Setup s = await SetupAsync();
        Ranges r = await RangesAsync();
        Authenticate(s.PplA);
        var id = Guid.CreateVersion7();

        (await PostEntryAsync(id, s.CycleA, Today, r.Medium, 500, 800m)).ShouldBe((HttpStatusCode.OK, id));
        (await PostEntryAsync(id, s.CycleA, Today, r.Medium, 500, 800m)).ShouldBe((HttpStatusCode.OK, id));

        // Another device reports the same range again: the first entry changes and keeps its id.
        (await PostEntryAsync(Guid.CreateVersion7(), s.CycleA, Today, r.Medium, 400, 640m)).ShouldBe((HttpStatusCode.OK, id));

        List<Entry> entries = (await HttpClient.GetFromJsonAsync<List<Entry>>($"production/live-bird-stocks?cycleId={s.CycleA}"))!;
        Entry entry = entries.ShouldHaveSingleItem();
        entry.Birds.ShouldBe(400);
        entry.AverageWeightKg.ShouldBe(1.6m);

        // The id is taken by another range; an average outside the range is refused.
        (await PostAsync(id, s.CycleA, Today, r.Large, 100, 200m)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        string outside = await (await PostAsync(null, s.CycleA, Today, r.Medium, 100, 200m)).Content.ReadAsStringAsync();
        outside.ShouldContain("LiveBirdStock.AverageOutsideRange");
    }

    [Fact]
    public async Task Upsert_Should_KeepAllRangesWithinThePopulation()
    {
        Setup s = await SetupAsync();
        Ranges r = await RangesAsync();
        Authenticate(s.PplA);

        (await PostEntryAsync(null, s.CycleA, Today, r.Small, 600, 720m)).Status.ShouldBe(HttpStatusCode.OK);
        (await PostEntryAsync(null, s.CycleA, Today, r.Medium, 400, 640m)).Status.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage tooMany = await PostAsync(null, s.CycleA, Today, r.Large, 1, 2m);
        tooMany.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await tooMany.Content.ReadAsStringAsync()).ShouldContain("LiveBirdStock.PopulationExceeded");

        // Lowering one range makes room for another.
        (await PostEntryAsync(null, s.CycleA, Today, r.Small, 500, 600m)).Status.ShouldBe(HttpStatusCode.OK);
        (await PostEntryAsync(null, s.CycleA, Today, r.Large, 100, 200m)).Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Scope_Should_LimitThePpl_AndKeepTheManagerReadOnly()
    {
        Setup s = await SetupAsync();
        Ranges r = await RangesAsync();

        Authenticate(s.PplB);
        (await PostAsync(null, s.CycleA, Today, r.Medium, 100, 160m)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        Authenticate(s.PplA);
        (await PostEntryAsync(null, s.CycleA, Today, r.Medium, 100, 160m)).Status.ShouldBe(HttpStatusCode.OK);

        Authenticate(s.PplB);
        (await HttpClient.GetFromJsonAsync<List<Entry>>($"production/live-bird-stocks?cycleId={s.CycleA}"))!.ShouldBeEmpty();

        Authenticate(s.Manager);
        (await PostAsync(null, s.CycleA, Today, r.Medium, 100, 160m)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await HttpClient.GetFromJsonAsync<List<Entry>>($"production/live-bird-stocks?cycleId={s.CycleA}"))!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Summary_Should_UseTheLatestReport_AndFlagOldData()
    {
        Setup s = await SetupAsync();
        Ranges r = await RangesAsync();
        Authenticate(s.PplA);
        (await PostEntryAsync(null, s.CycleA, Today.AddDays(-1), r.Medium, 300, 480m)).Status.ShouldBe(HttpStatusCode.OK);
        (await PostEntryAsync(null, s.CycleA, Today.AddDays(-1), r.Large, 200, 400m)).Status.ShouldBe(HttpStatusCode.OK);

        Authenticate(s.Manager);
        Summary today = await SummaryAsync(s, Today);
        CoopSummary coopA = today.Coops.Single(c => c.CycleId == s.CycleA);
        coopA.ReportDate.ShouldBe(Today.AddDays(-1));
        coopA.DataAgeDays.ShouldBe(1);
        coopA.IsStale.ShouldBeFalse();
        coopA.TotalBirds.ShouldBe(500);
        today.Coops.Single(c => c.CycleId == s.CycleB).ReportDate.ShouldBeNull();
        today.Ranges.Single(x => x.WeightRangeId == r.Medium).ShouldBe(new RangeTotal(r.Medium, 300, 480m, 1));
        today.TotalBirds.ShouldBe(500);

        (await SummaryAsync(s, Today.AddDays(1))).Coops.Single(c => c.CycleId == s.CycleA).IsStale.ShouldBeTrue();
        (await SummaryAsync(s, Today.AddDays(-2))).Coops.Single(c => c.CycleId == s.CycleA).ReportDate.ShouldBeNull();
    }

    [Fact]
    public async Task FieldContext_Should_CarryTheRanges_AndTheLatestReport()
    {
        Setup s = await SetupAsync();
        Ranges r = await RangesAsync();
        Authenticate(s.PplA);
        (await PostEntryAsync(null, s.CycleA, Today.AddDays(-1), r.Medium, 300, 480m)).Status.ShouldBe(HttpStatusCode.OK);

        FieldContext context = (await HttpClient.GetFromJsonAsync<FieldContext>("mobile/field-context"))!;

        context.WeightRanges.Select(x => x.Id).ShouldContain(r.Medium);
        FieldCycle cycle = context.Cycles.ShouldHaveSingleItem();
        cycle.LatestStockDate.ShouldBe(Today.AddDays(-1));
        cycle.LatestStock.ShouldHaveSingleItem().Birds.ShouldBe(300);
    }

    [Fact]
    public async Task Delete_Should_OnlyAcceptRecentEntries()
    {
        Setup s = await SetupAsync();
        Ranges r = await RangesAsync();
        Authenticate(s.PplA);
        (Guid recent, Guid old) = ((await PostEntryAsync(null, s.CycleA, Today, r.Medium, 300, 480m)).Id,
            (await PostEntryAsync(null, s.CycleA, Today.AddDays(-3), r.Medium, 300, 480m)).Id);

        (await HttpClient.DeleteAsync($"production/live-bird-stocks/{recent}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        HttpResponseMessage tooOld = await HttpClient.DeleteAsync($"production/live-bird-stocks/{old}");
        tooOld.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await tooOld.Content.ReadAsStringAsync()).ShouldContain("LiveBirdStock.TooOldToDelete");
    }

    /// <summary>
    /// Three active ranges shared by every test (active ranges are global and may not overlap).
    /// </summary>
    private async Task<Ranges> RangesAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        async Task<Guid> GetOrCreateAsync(string code, decimal? min, decimal? max, int sortOrder)
        {
            if (await db.WeightRanges.SingleOrDefaultAsync(x => x.Code == code) is { } existing)
            {
                return existing.Id;
            }

            WeightRange range = WeightRange.Create(code, code, min, max, sortOrder).Value;
            db.WeightRanges.Add(range);
            await db.SaveChangesAsync();

            return range.Id;
        }

        return new Ranges(
            await GetOrCreateAsync("IT-S", null, 1.4m, 1),
            await GetOrCreateAsync("IT-M", 1.4m, 1.8m, 2),
            await GetOrCreateAsync("IT-L", 1.8m, null, 3));
    }

    private async Task<Summary> SummaryAsync(Setup s, DateOnly date) =>
        (await HttpClient.GetFromJsonAsync<Summary>($"production/live-bird-stocks/summary?date={date:yyyy-MM-dd}&branchId={s.Branch}"))!;

    private async Task<(HttpStatusCode Status, Guid Id)> PostEntryAsync(
        Guid? id, Guid cycleId, DateOnly date, Guid weightRangeId, int birds, decimal weightKg)
    {
        HttpResponseMessage response = await PostAsync(id, cycleId, date, weightRangeId, birds, weightKg);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
        }

        return (response.StatusCode, await response.Content.ReadFromJsonAsync<Guid>());
    }

    private Task<HttpResponseMessage> PostAsync(Guid? id, Guid cycleId, DateOnly date, Guid weightRangeId, int birds, decimal weightKg) =>
        HttpClient.PostAsJsonAsync("production/live-bird-stocks", new { id, cycleId, date, weightRangeId, birds, weightKg });
}
