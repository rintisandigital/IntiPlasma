using Application.Mobile;
using Application.Production;
using Domain.Partnership.Cycles;

namespace Application.UnitTests.Mobile;

public sealed class MobileDashboardCalculatorTests
{
    private static readonly DateOnly ChickIn = new(2026, 9, 10);
    private static readonly DateOnly Today = new(2026, 10, 9);
    private static readonly MobileDashboardOptions Options = new();

    [Fact]
    public void BuildDays_Should_CarryBodyWeight_AndCountHarvestFromItsDate()
    {
        List<DailyPerformance> days = CyclePerformanceBuilder.BuildDays(
            1_000,
            [
                new RecordedDay(ChickIn.AddDays(1), 1, 10, 0, 50m, 200m),
                new RecordedDay(ChickIn.AddDays(2), 2, 5, 5, 60m, null)
            ],
            [new HarvestedBatch(ChickIn.AddDays(2), 100, 50m)]);

        days[0].Cumulative.ShouldBe(CyclePerformance.Calculate(1_000, 10, 0, 0, 0, 50m, 0.2m, 1));
        days[1].Cumulative.ShouldBe(CyclePerformance.Calculate(1_000, 15, 5, 100, 50m, 110m, 0.2m, 2));
        CyclePerformanceBuilder.Current(1_000, days).ShouldBe(days[1].Cumulative);
        CyclePerformanceBuilder.Current(1_000, []).Population.ShouldBe(1_000);
    }

    [Fact]
    public void Cycle_RecordedToday_Should_HaveNoFlags()
    {
        DashboardCycleResponse cycle = Build(
            Cycle(ageDays: 10),
            [Day(1, feedKg: 100), Day(0, feedKg: 100)],
            feedKg: 1_000);

        cycle.RecordedToday.ShouldBeTrue();
        cycle.RecordedYesterday.ShouldBeTrue();
        cycle.AgeDays.ShouldBe(10);
        cycle.Flags.ShouldBeEmpty();
    }

    [Fact]
    public void Cycle_Should_FlagLateRecording_AfterMoreThanOneDay()
    {
        Build(Cycle(ageDays: 10), [Day(1)], feedKg: 0).Flags.ShouldNotContain(DashboardFlags.RecordingLate);
        Build(Cycle(ageDays: 10), [Day(2)], feedKg: 0).Flags.ShouldContain(DashboardFlags.RecordingLate);

        // Without any recording the chick-in date counts.
        Build(Cycle(ageDays: 1), [], feedKg: 0).Flags.ShouldNotContain(DashboardFlags.RecordingLate);
        Build(Cycle(ageDays: 2), [], feedKg: 0).Flags.ShouldContain(DashboardFlags.RecordingLate);
    }

    [Fact]
    public void Cycle_Should_AverageLatestFeedDays_AndEstimateDaysLeft()
    {
        DashboardCycleResponse cycle = Build(
            Cycle(ageDays: 10),
            [Day(4, feedKg: 999), Day(3, feedKg: 90), Day(2, feedKg: 0), Day(1, feedKg: 100), Day(0, feedKg: 110)],
            feedKg: 250);

        // Today, yesterday and three days ago: the day without feed usage is skipped.
        cycle.AverageDailyFeedKg.ShouldBe(100m);
        cycle.FeedDaysLeft.ShouldBe(2.5m);
        cycle.Flags.ShouldContain(DashboardFlags.FeedLow);
    }

    [Fact]
    public void Cycle_WithoutFeedUsage_Should_NotEstimateDaysLeft()
    {
        DashboardCycleResponse cycle = Build(Cycle(ageDays: 1), [Day(0, feedKg: 0)], feedKg: 0);

        cycle.AverageDailyFeedKg.ShouldBeNull();
        cycle.FeedDaysLeft.ShouldBeNull();
        cycle.Flags.ShouldNotContain(DashboardFlags.FeedLow);
    }

    [Fact]
    public void Cycle_Should_FlagHighDepletion_FromThreshold()
    {
        Build(Cycle(ageDays: 10), [Day(0, mortality: 49)], feedKg: 0).Flags.ShouldNotContain(DashboardFlags.HighDepletion);
        Build(Cycle(ageDays: 10), [Day(0, mortality: 50)], feedKg: 0).Flags.ShouldContain(DashboardFlags.HighDepletion);
    }

    [Fact]
    public void Cycle_Should_ExpectStockReport_FromAge21()
    {
        Build(Cycle(ageDays: 20), [Day(0)], feedKg: 0).Flags.ShouldNotContain(DashboardFlags.NoStockReport);
        Build(Cycle(ageDays: 21), [Day(0)], feedKg: 0).Flags.ShouldContain(DashboardFlags.NoStockReport);

        DashboardCycleResponse reported = Build(Cycle(ageDays: 21) with { LatestStockDate = Today }, [Day(0)], feedKg: 0);
        reported.StockReportedToday.ShouldBeTrue();
        reported.Flags.ShouldNotContain(DashboardFlags.NoStockReport);
    }

    [Fact]
    public void Kpi_Should_CombineCycles()
    {
        // A: 1.000 birds, 20 dead, BW 1 kg, feed 1.470 kg → FCR 1,5; B: no body weight yet, left out of FCR and IP.
        DashboardCycleResponse a = Build(
            Cycle(ageDays: 21) with { Population = 980, LatestStockDate = Today },
            [Day(1, mortality: 20, feedKg: 1_470, bodyWeightGram: 1_000), Day(0)],
            feedKg: 0);
        DashboardCycleResponse b = Build(
            Cycle(ageDays: 5) with { InitialPopulation = 3_000, Population = 2_970 },
            [Day(0, mortality: 30, feedKg: 100)],
            feedKg: 500);

        DashboardKpi kpi = MobileDashboardCalculator.BuildKpi([a, b], Options);

        kpi.ActiveCycles.ShouldBe(2);
        kpi.InitialPopulation.ShouldBe(4_000);
        kpi.Population.ShouldBe(3_950);
        kpi.DepletionPercent.ShouldBe(1.25m);
        kpi.Fcr.ShouldBe(1.5m);
        kpi.Ip.ShouldBe(a.Performance.Ip);
        kpi.RecordedToday.ShouldBe(2);
        kpi.StockReportExpected.ShouldBe(1);
        kpi.StockReportedToday.ShouldBe(1);
        kpi.FeedStockKg.ShouldBe(500m);
    }

    [Fact]
    public void Kpi_WithoutCycles_Should_BeEmpty()
    {
        DashboardKpi kpi = MobileDashboardCalculator.BuildKpi([], Options);

        kpi.ActiveCycles.ShouldBe(0);
        kpi.DepletionPercent.ShouldBe(0m);
        kpi.Fcr.ShouldBeNull();
        kpi.Ip.ShouldBeNull();
    }

    private static DashboardCycleResponse Build(
        GetMobileDashboardQueryHandler.CycleRow cycle,
        IReadOnlyList<RecordedDay> days,
        decimal feedKg) =>
        MobileDashboardCalculator.BuildCycle(
            cycle,
            Today,
            days,
            [],
            feedKg == 0 ? [] : [new DashboardFeedStock(Guid.NewGuid(), "PK", "Pakan", feedKg, "KG", "SAK", 50m)],
            Options);

    private static GetMobileDashboardQueryHandler.CycleRow Cycle(int ageDays) => new()
    {
        CycleId = Guid.NewGuid(),
        Number = "SKL/1",
        Status = "Active",
        BranchCode = "BDG",
        CoopCode = "K1",
        CoopName = "Kandang 1",
        FarmerName = "Peternak",
        ChickInDate = Today.AddDays(-ageDays),
        InitialPopulation = 1_000,
        Population = 1_000
    };

    /// <param name="daysAgo">0 = today; tests list the days oldest first.</param>
    private static RecordedDay Day(int daysAgo, int mortality = 0, decimal feedKg = 0, decimal? bodyWeightGram = null) =>
        new(Today.AddDays(-daysAgo), 20 - daysAgo, mortality, 0, feedKg, bodyWeightGram);
}
