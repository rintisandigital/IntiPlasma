using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Production;

/// <summary>
/// PLAN-MOBILE M4: the mobile dashboard follows the caller's scope and today's recordings (M-55 s.d. M-60).
/// </summary>
public sealed class MobileDashboardTests(IntegrationTestWebAppFactory factory) : FieldScenarioTest(factory)
{
    private sealed record Dashboard(DateOnly Date, Kpi Kpi, List<DashboardCycle> Cycles);

    private sealed record Kpi(int ActiveCycles, int InitialPopulation, int Population, decimal DepletionPercent, int RecordedToday, decimal FeedStockKg);

    private sealed record DashboardCycle(
        Guid CycleId,
        int AgeDays,
        int Population,
        Performance Performance,
        bool RecordedToday,
        decimal FeedStockKg,
        decimal? AverageDailyFeedKg,
        decimal? FeedDaysLeft,
        List<FeedStock> FeedStock,
        List<string> Flags);

    private sealed record Performance(int Mortality, int Culling, decimal DepletionPercent, decimal FeedKg, decimal? AverageWeightKg);

    private sealed record FeedStock(Guid ItemId, decimal Quantity, string BaseUomCode, string? PackUomCode, decimal? PackFactor);

    [Fact]
    public async Task Dashboard_Should_FollowScope_AndTodaysRecording()
    {
        Setup s = await SetupAsync();

        Authenticate(s.Manager);
        Dashboard manager = await GetAsync(Today);
        manager.Cycles.Select(c => c.CycleId).ShouldBe([s.CycleA, s.CycleB], ignoreOrder: true);
        manager.Kpi.ActiveCycles.ShouldBe(2);
        manager.Kpi.InitialPopulation.ShouldBe(2_000);
        manager.Kpi.RecordedToday.ShouldBe(0);
        manager.Kpi.FeedStockKg.ShouldBe(2_000m);

        Authenticate(s.PplA);
        DashboardCycle before = (await GetAsync(Today)).Cycles.ShouldHaveSingleItem();
        before.CycleId.ShouldBe(s.CycleA);
        before.AgeDays.ShouldBe(3);
        before.Flags.ShouldBe(["RecordingLate"]);
        before.FeedStockKg.ShouldBe(1_000m);
        before.FeedDaysLeft.ShouldBeNull();
        before.FeedStock.ShouldBe([new FeedStock(s.Feed, 1_000m, "KG", "SAK", 50m)]);

        // 2 SAK (100 kg), 5 dead, 1 culled, BW 180 g.
        using var request = new HttpRequestMessage(HttpMethod.Post, "production/daily-recordings")
        {
            Content = JsonContent.Create(new
            {
                id = Guid.CreateVersion7(),
                cycleId = s.CycleA,
                date = Today,
                mortality = 5,
                culling = 1,
                averageBodyWeightGram = 180m,
                usages = new[] { new { itemId = s.Feed, uomId = s.Sak, quantity = 2m } },
                documents = Array.Empty<Guid>()
            })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        (await HttpClient.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.OK);

        Dashboard after = await GetAsync(Today);
        DashboardCycle cycle = after.Cycles.ShouldHaveSingleItem();
        cycle.RecordedToday.ShouldBeTrue();
        cycle.Flags.ShouldBeEmpty();
        cycle.Population.ShouldBe(994);
        cycle.Performance.ShouldBe(new Performance(5, 1, 0.6m, 100m, 0.18m));
        cycle.FeedStockKg.ShouldBe(900m);
        cycle.AverageDailyFeedKg.ShouldBe(100m);
        cycle.FeedDaysLeft.ShouldBe(9m);
        after.Kpi.RecordedToday.ShouldBe(1);
        after.Kpi.DepletionPercent.ShouldBe(0.6m);

        // PPL B still has nothing recorded.
        Authenticate(s.PplB);
        (await GetAsync(Today)).Cycles.ShouldHaveSingleItem().RecordedToday.ShouldBeFalse();
    }

    [Fact]
    public async Task Dashboard_Should_RejectFutureDate_AndInaccessibleBranch()
    {
        Setup s = await SetupAsync();
        Authenticate(s.Manager);

        HttpResponseMessage future = await HttpClient.GetAsync($"mobile/dashboard?date={Today.AddDays(2):yyyy-MM-dd}");
        future.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await future.Content.ReadAsStringAsync()).ShouldContain("MobileDashboard.FutureDate");

        (await HttpClient.GetAsync($"mobile/dashboard?branchId={Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        HttpResponseMessage branch = await HttpClient.GetAsync($"mobile/dashboard?date={Today:yyyy-MM-dd}&branchId={s.Branch}");
        branch.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await branch.Content.ReadFromJsonAsync<Dashboard>())!.Cycles.Count.ShouldBe(2);
    }

    private async Task<Dashboard> GetAsync(DateOnly date)
    {
        HttpResponseMessage response = await HttpClient.GetAsync($"mobile/dashboard?date={date:yyyy-MM-dd}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<Dashboard>())!;
    }
}
