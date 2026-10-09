using MobileApp.Core.Api;
using MobileApp.Core.Contracts;
using MobileApp.Core.Local;

namespace MobileApp.UnitTests.Api;

/// <summary>
/// Dashboard and performance endpoints (PLAN-MOBILE M4, M-63): one cache key for the dashboard across days.
/// </summary>
public sealed class DashboardApiTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"intiplasma-dashboard-{Guid.NewGuid():N}.db3");
    private static readonly string[] FeedLowFlag = [DashboardFlags.FeedLow];

    private bool _online = true;

    [Fact]
    public async Task Dashboard_Should_UseTheServerDate_AndBeAvailableOffline()
    {
        using var api = new FakeApi(Respond);
        using var db = new LocalDb(_path);
        var production = new ProductionApi(new ApiCache(api.Client, db), api.Client);

        CachedResult<MobileDashboard> online = await production.GetDashboardAsync();
        _online = false;
        CachedResult<MobileDashboard> offline = await production.GetDashboardAsync();

        api.Api.Requests[0].Path.ShouldBe("/api/v1/mobile/dashboard");
        online.Value.Kpi.ActiveCycles.ShouldBe(1);
        DashboardCycle cycle = online.Value.Cycles.Single();
        cycle.Performance.Fcr.ShouldBe(1.5m);
        cycle.Flags.ShouldBe([DashboardFlags.FeedLow]);
        cycle.FeedStock.Single().PackUomCode.ShouldBe("SAK");
        offline.IsFromCache.ShouldBeTrue();
        offline.Value.Cycles.Single().CoopName.ShouldBe("Kandang A");
    }

    [Fact]
    public async Task Performance_Should_ReadTheCycleDays()
    {
        using var api = new FakeApi(Respond);
        using var db = new LocalDb(_path);
        var production = new ProductionApi(new ApiCache(api.Client, db), api.Client);
        var cycleId = Guid.NewGuid();

        CachedResult<CyclePerformanceReport> result = await production.GetPerformanceAsync(cycleId);

        api.Api.Requests.Single().Path.ShouldBe($"/api/v1/cycles/{cycleId}/performance");
        DailyPerformance day = result.Value.Days.Single();
        day.AgeDays.ShouldBe(1);
        day.Cumulative.DepletionPercent.ShouldBe(0.1m);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    private static object Figures(decimal? fcr) => new
    {
        initialPopulation = 1_000,
        mortality = 1,
        culling = 0,
        harvestedBirds = 0,
        harvestedWeightKg = 0,
        population = 999,
        feedKg = 10,
        depletionPercent = 0.1m,
        averageWeightKg = 0.04m,
        liveWeightKg = 39.96m,
        fcr,
        ageDays = 1,
        adgGram = 40,
        ip = (decimal?)null
    };

    private HttpResponseMessage Respond(HttpRequestMessage request, string? body)
    {
        if (!_online)
        {
            return StubHttpHandler.Offline(request, body);
        }

        if (request.RequestUri!.AbsolutePath.EndsWith("/performance", StringComparison.Ordinal))
        {
            return StubHttpHandler.Json(new
            {
                cycleId = Guid.NewGuid(),
                number = "SKL/1",
                status = "Active",
                chickInDate = "2026-10-01",
                current = Figures(null),
                days = new[] { new { date = "2026-10-02", ageDays = 1, mortality = 1, culling = 0, feedKg = 10, averageBodyWeightGram = 40, cumulative = Figures(null) } },
                harvests = Array.Empty<object>()
            });
        }

        return StubHttpHandler.Json(new
        {
            date = "2026-10-09",
            generatedAtUtc = "2026-10-09T03:00:00Z",
            thresholds = new { feedWarningDays = 3, recordingLateDays = 1, depletionWarningPercent = 5, stockReportFromAgeDays = 21 },
            kpi = new { activeCycles = 1, initialPopulation = 1_000, population = 999 },
            cycles = new[]
            {
                new
                {
                    cycleId = Guid.NewGuid(),
                    number = "SKL/1",
                    status = "Active",
                    coopCode = "KA",
                    coopName = "Kandang A",
                    chickInDate = "2026-10-01",
                    ageDays = 8,
                    population = 999,
                    performance = Figures(1.5m),
                    feedStockKg = 100,
                    averageDailyFeedKg = 50,
                    feedDaysLeft = 2,
                    feedStock = new[] { new { itemId = Guid.NewGuid(), itemCode = "PK", itemName = "Pakan", quantity = 100, baseUomCode = "KG", packUomCode = "SAK", packFactor = 50 } },
                    flags = FeedLowFlag
                }
            }
        });
    }
}
