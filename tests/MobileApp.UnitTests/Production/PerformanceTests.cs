using MobileApp.Core.Contracts;
using MobileApp.Core.Local;
using MobileApp.Core.Production;

namespace MobileApp.UnitTests.Production;

/// <summary>
/// Charts and dashboard on the device (PLAN-MOBILE M4): the ported formulas (M-65), queued recordings in the chart,
/// weeks (M-68), chart series (M-64, M-66) and today's tasks (M-61).
/// </summary>
public sealed class PerformanceTests
{
    private static readonly DateOnly ChickIn = new(2026, 10, 1);
    private static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void Calculate_Should_MatchTheServerFormulas()
    {
        // Same numbers as Domain.UnitTests ProductionTests.Performance_Should_MatchTheIndustryFormulas.
        PerformanceFigures performance = PerformanceCalculator.Calculate(10_000, 400, 100, 9_500, 19_950m, 31_920m, 2.1m, 35m);

        performance.Population.ShouldBe(0);
        performance.DepletionPercent.ShouldBe(5m);
        performance.Fcr.ShouldBe(1.6m);
        performance.Ip.ShouldBe(356.25m);
        performance.AdgGram.ShouldBe(60m);
    }

    [Fact]
    public void WithLocal_WithoutQueue_Should_KeepTheServerDays()
    {
        CyclePerformanceReport report = Report(Days(1, 8));

        IReadOnlyList<PerformancePoint> points = PerformanceCalculator.WithLocal(report, [], null);

        points.Count.ShouldBe(8);
        points.ShouldAllBe(p => !p.IsLocal);
        points[^1].Cumulative.ShouldBe(report.Days[^1].Cumulative);
    }

    [Fact]
    public void WithLocal_Should_AddQueuedDays_WithTheSameFormulas()
    {
        CyclePerformanceReport report = Report(Days(1, 7));
        RecordingDraft queued = Sample.Draft(); // 09/10: 2 dead, 1 culled, BW 180 g, 1 SAK (50 kg)

        IReadOnlyList<PerformancePoint> points = PerformanceCalculator.WithLocal(
            report,
            [queued, queued with { Id = Guid.NewGuid(), CycleId = Guid.NewGuid() }],
            Sample.Context());

        points.Count.ShouldBe(8);
        PerformancePoint local = points[^1];
        local.IsLocal.ShouldBeTrue();
        local.AgeDays.ShouldBe(8);
        local.FeedKg.ShouldBe(50m);
        local.Cumulative.ShouldBe(PerformanceCalculator.Calculate(1_000, 7 + 2, 1, 0, 0, 7 * 10m + 50m, 0.18m, 8));
        points[^2].Cumulative.ShouldBe(report.Days[^1].Cumulative);
    }

    [Fact]
    public void Weeks_Should_SumFlows_AndTakePositionsFromTheLastDay()
    {
        // Ages 1–10: 1 dead and 10 kg a day, BW = 40 g × age.
        IReadOnlyList<PerformancePoint> days = PerformanceCalculator.WithLocal(Report(Days(1, 10)), [], null);

        IReadOnlyList<WeekPerformance> weeks = WeeklyPerformance.Aggregate(days, 1_000);

        weeks.Count.ShouldBe(2);
        WeekPerformance first = weeks[0];
        (first.Week, first.FromAge, first.ToAge, first.Days, first.IsComplete).ShouldBe((1, 1, 7, 7, true));
        first.FeedKg.ShouldBe(70m);
        first.FeedGramPerBird.ShouldBe(70m);
        first.Depleted.ShouldBe(7);
        first.DepletedPercent.ShouldBe(0.7m);
        first.BodyWeightGram.ShouldBe(280m);
        first.BodyWeightGainGram.ShouldBeNull();

        WeekPerformance running = weeks[1];
        (running.Week, running.Days, running.IsComplete).ShouldBe((2, 3, false));
        running.FeedGramPerBird.ShouldBe(30.211m); // 30 kg over the 993 birds alive after week 1
        running.BodyWeightGainGram.ShouldBe(120m);
        running.Cumulative.ShouldBe(days[^1].Cumulative);
    }

    [Fact]
    public void Weeks_Should_PutTheChickInDayInWeekOne()
    {
        WeekPerformance.WeekOf(0).ShouldBe(1);
        WeekPerformance.WeekOf(7).ShouldBe(1);
        WeekPerformance.WeekOf(8).ShouldBe(2);
        WeekPerformance.WeekOf(35).ShouldBe(5);
    }

    [Fact]
    public void Chart_OfOneCycle_Should_ShowDailyAndCumulativeFeed_AndMarkQueuedDays()
    {
        CycleSeries cycle = Series(Report(Days(1, 7)), [Sample.Draft()]);

        ChartSpec chart = PerformanceCharts.Build(ChartMetric.Feed, weekly: false, [cycle]);

        chart.Labels.ShouldBe(["1", "2", "3", "4", "5", "6", "7", "8"]);
        chart.Datasets.Select(d => (d.Type, d.Axis)).ShouldBe([("bar", "y"), ("line", "y1")]);
        chart.Datasets[0].Data[^1].ShouldBe(50m);
        chart.Datasets[1].Data[^1].ShouldBe(7 * 10m + 50m);
        chart.Datasets[0].LocalFrom.ShouldBe(7);
        chart.Y1Title.ShouldNotBeNull();
    }

    [Fact]
    public void Chart_Comparing_Should_AlignCyclesByAge_OneLineEach()
    {
        CycleSeries a = Series(Report(Days(1, 3)), []);
        CycleSeries b = Series(Report(Days(2, 5)) with { CycleId = Guid.NewGuid() }, []) with { Label = "B" };

        ChartSpec chart = PerformanceCharts.Build(ChartMetric.BodyWeight, weekly: false, [a, b]);

        chart.Labels.ShouldBe(["1", "2", "3", "4", "5"]);
        chart.Datasets.Select(d => d.Color).ShouldBe(PerformanceCharts.CompareColors.Take(2));
        chart.Datasets[0].Data.ShouldBe([40m, 80m, 120m, null, null]);
        chart.Datasets[1].Data.ShouldBe([null, 80m, 120m, 160m, 200m]);
        chart.Datasets.ShouldAllBe(d => d.LocalFrom == null);
    }

    [Fact]
    public void Chart_PerWeek_Should_LabelWeeks()
    {
        ChartSpec chart = PerformanceCharts.Build(ChartMetric.Mortality, weekly: true, [Series(Report(Days(1, 10)), [])]);

        chart.Labels.ShouldBe(["M1", "M2"]);
        chart.Datasets[0].Data.ShouldBe([7m, 3m]);
        chart.Datasets[1].Data.ShouldBe([0.7m, 0.302m]);
    }

    [Fact]
    public void Dashboard_Should_MergeTheQueue_IntoTodaysTasks_AndFlags()
    {
        DashboardCycle recorded = DashboardCycle(age: 25) with
        {
            LastRecordingDate = Today,
            LatestStockDate = Today,
            Flags = []
        };
        DashboardCycle queued = DashboardCycle(age: 25) with
        {
            CycleId = Sample.CycleId,
            LastRecordingDate = Today.AddDays(-3),
            Flags = [DashboardFlags.RecordingLate, DashboardFlags.NoStockReport, DashboardFlags.FeedLow]
        };
        DashboardCycle young = DashboardCycle(age: 5) with { LastRecordingDate = Today.AddDays(-1) };
        var dashboard = new MobileDashboard { Date = Today, Cycles = [recorded, queued, young] };

        LocalRecording local = new(Item(SyncStatus.Pending), Sample.Draft());

        DashboardView view = DashboardService.Merge(dashboard, null, [local], [], Today);

        view.Tasks.Select(t => (t.Recording, t.Stock)).ShouldBe(
        [
            (TaskState.Done, TaskState.Done),
            (TaskState.Queued, TaskState.Missing),
            (TaskState.Missing, TaskState.NotExpected)
        ]);
        view.Tasks[1].Flags.ShouldBe([DashboardFlags.NoStockReport, DashboardFlags.FeedLow]);
        view.AtRisk.ShouldBe([view.Tasks[1]]);
    }

    [Fact]
    public void Dashboard_Should_ShowAFailedQueuedEntry()
    {
        var dashboard = new MobileDashboard { Date = Today, Cycles = [DashboardCycle(age: 8) with { CycleId = Sample.CycleId }] };

        DashboardView view = DashboardService.Merge(dashboard, null, [new LocalRecording(Item(SyncStatus.Failed), Sample.Draft())], [], Today);

        view.Tasks.Single().Recording.ShouldBe(TaskState.Failed);
    }

    private static SyncItem Item(SyncStatus status) => new() { Id = Guid.NewGuid(), CycleId = Sample.CycleId, Date = Today, Status = status };

    private static DashboardCycle DashboardCycle(int age) => new()
    {
        CycleId = Guid.NewGuid(),
        CoopCode = "K",
        ChickInDate = Today.AddDays(-age)
    };

    private static CycleSeries Series(CyclePerformanceReport report, IReadOnlyList<RecordingDraft> queued) =>
        new(report.CycleId, "A", 1_000, PerformanceCalculator.WithLocal(report, queued, Sample.Context()));

    /// <summary>
    /// Server days for ages <paramref name="from"/>–<paramref name="to"/>: 1 dead and 10 kg feed a day, BW 40 g × age,
    /// accumulated with the same formulas.
    /// </summary>
    private static List<DailyPerformance> Days(int from, int to)
    {
        var days = new List<DailyPerformance>();
        int dead = 0;
        decimal feed = 0;

        for (int age = from; age <= to; age++)
        {
            dead++;
            feed += 10m;
            decimal bodyWeight = 40m * age;
            days.Add(new DailyPerformance(
                ChickIn.AddDays(age),
                age,
                1,
                0,
                10m,
                bodyWeight,
                PerformanceCalculator.Calculate(1_000, dead, 0, 0, 0, feed, bodyWeight / 1000m, age)));
        }

        return days;
    }

    private static CyclePerformanceReport Report(List<DailyPerformance> days) => new()
    {
        CycleId = Sample.CycleId,
        Number = "SKL/BDG/2026/X/0001",
        Status = "Active",
        ChickInDate = ChickIn,
        Current = days.Count > 0 ? days[^1].Cumulative : PerformanceCalculator.Calculate(1_000, 0, 0, 0, 0, 0, null, null),
        Days = days
    };
}
