using MobileApp.Core.Contracts;
using MobileApp.Core.Formatting;
using MobileApp.Core.Production;

namespace MobileApp.UnitTests.Production;

/// <summary>
/// Breed standard on the device (PLAN-MOBILE M5): the ported interpolation and deviation (same numbers as the
/// Domain tests), queued recordings judged against the standard, and the standard line of the charts (M-78).
/// </summary>
public sealed class StandardTests
{
    private static readonly DateOnly ChickIn = new(2026, 10, 1);

    private static readonly PerformanceStandard Standard = new()
    {
        Id = Guid.NewGuid(),
        Code = "STD-1",
        Name = "CP 707",
        Rows =
        [
            new StandardRow(0, 42m, null, 0m, null, 0m),
            new StandardRow(7, 190m, 0.85m, 0.8m, null, 160m),
            new StandardRow(14, 480m, 1.05m, 1.5m, null, 520m)
        ]
    };

    [Fact]
    public void ValueAt_Should_MatchTheServerInterpolation()
    {
        // Same numbers as Domain.UnitTests BreedStandardTests.ValueAt_Should_Interpolate_BetweenRows_AndStopAtTheEnds.
        StandardValue day10 = StandardCalculator.ValueAt(Standard, 10)!;
        day10.BodyWeightGram.ShouldBe(314.286m);
        day10.Fcr.ShouldBe(0.936m);
        day10.DepletionPercent.ShouldBe(1.1m);
        day10.CumulativeFeedGram.ShouldBe(314.286m);

        StandardValue day3 = StandardCalculator.ValueAt(Standard, 3)!;
        day3.Fcr.ShouldBeNull();
        day3.BodyWeightGram.ShouldBe(105.429m);

        StandardCalculator.ValueAt(Standard, 15).ShouldBeNull();
    }

    [Fact]
    public void Deviation_Should_MatchTheServerFormulas()
    {
        // Same numbers as Domain.UnitTests BreedStandardTests.Deviation_Should_FollowTheFormulas_AndThresholds.
        StandardDeviation deviation = StandardCalculator.Deviation(new StandardValue(35, 2_000m, 1.5m, 4m, 3_000m), 1_880m, 1.56m, 5.2m, 3_150m);

        deviation.BodyWeightPercent.ShouldBe(-6m);
        deviation.Fcr.ShouldBe(0.06m);
        deviation.DepletionPoints.ShouldBe(1.2m);
        deviation.CumulativeFeedPercent.ShouldBe(5m);
        deviation.IsBodyWeightBelow(StandardThresholds.Default).ShouldBeTrue();
    }

    [Fact]
    public void Latest_Should_JudgeAQueuedRecording()
    {
        // Server days 1-7 (BW 40 g × age) plus the queued day 8 with 180 g, against 231,429 g interpolated.
        IReadOnlyList<PerformancePoint> days = PerformanceCalculator.WithLocal(Report(), [Sample.Draft()], Sample.Context());

        StandardStatus status = StandardCalculator.Latest(days, Standard, 1_000)!;

        status.WeighingAgeDays.ShouldBe(8);
        status.StandardBodyWeightGram.ShouldBe(231.429m);
        status.BodyWeightPercent.ShouldBe(-22.222m);
        status.BodyWeightBelow.ShouldBeTrue();
        status.BelowStandard.ShouldBeTrue();

        StandardCalculator.Latest(days, null, 1_000).ShouldBeNull();
    }

    [Fact]
    public void DayDeviation_Should_SkipBodyWeight_WithoutAWeighing()
    {
        PerformancePoint day = Points()[^1] with { AverageBodyWeightGram = null };

        StandardDeviation deviation = StandardCalculator.DayDeviation(Standard, day, 1_000)!;

        deviation.BodyWeightPercent.ShouldBeNull();
        deviation.Fcr.ShouldBeNull();
        deviation.DepletionPoints.ShouldBe(-0.1m); // 0,7% dead at age 7 against 0,8%
    }

    [Fact]
    public void Chart_Should_DrawTheStandard_AWeekAhead()
    {
        CycleSeries cycle = new(Sample.CycleId, "A", 1_000, Points());

        ChartSpec chart = PerformanceCharts.Build(ChartMetric.BodyWeight, weekly: false, [cycle], Standard);

        chart.Labels.Count.ShouldBe(14); // ages 1-7 recorded, 8-14 ahead (the standard ends at 14)
        ChartDataset standard = chart.Datasets[^1];
        standard.IsStandard.ShouldBeTrue();
        standard.Label.ShouldBe("Standar CP 707");
        standard.Data[0].ShouldBe(63.143m);
        standard.Data[^1].ShouldBe(480m);
        chart.Datasets[0].Data[^1].ShouldBeNull();
    }

    [Fact]
    public void Chart_Should_ShowTheFeedStandard_InKilograms_ForOneCycle()
    {
        CycleSeries cycle = new(Sample.CycleId, "A", 1_000, Points());

        ChartDataset feed = PerformanceCharts.Build(ChartMetric.Feed, weekly: false, [cycle], Standard).Datasets[^1];
        ChartDataset weekly = PerformanceCharts.Build(ChartMetric.BodyWeight, weekly: true, [cycle], Standard).Datasets[^1];

        feed.Axis.ShouldBe("y1");
        feed.Data[6].ShouldBe(160m); // 160 g × 1.000 birds at age 7
        weekly.Data.ShouldBe([190m, 480m]); // weeks 1-2 at age 7 and 14
        PerformanceCharts.Build(ChartMetric.Mortality, weekly: false, [cycle], Standard).Datasets.ShouldNotContain(d => d.IsStandard);
    }

    [Fact]
    public void BelowStandard_Should_HaveAnIndonesianLabel_AndSignedDeviations()
    {
        Labels.RiskFlag(DashboardFlags.BelowStandard).ShouldBe("Di bawah standar");
        IdFormat.Deviation(-3.6m, 1, "%").ShouldBe("-3,6%");
        IdFormat.Deviation(0.06m, 3).ShouldBe("+0,060");
        IdFormat.Deviation(null, 1).ShouldBe("–");
    }

    private static IReadOnlyList<PerformancePoint> Points() => PerformanceCalculator.WithLocal(Report(), [], null);

    /// <summary>
    /// Ages 1-7: 1 dead and 10 kg feed a day, BW 40 g × age.
    /// </summary>
    private static CyclePerformanceReport Report()
    {
        var days = new List<DailyPerformance>();
        decimal feed = 0;

        for (int age = 1; age <= 7; age++)
        {
            feed += 10m;
            days.Add(new DailyPerformance(
                ChickIn.AddDays(age), age, 1, 0, 10m, 40m * age,
                PerformanceCalculator.Calculate(1_000, age, 0, 0, 0, feed, 40m * age / 1000m, age)));
        }

        return new CyclePerformanceReport
        {
            CycleId = Sample.CycleId,
            Number = "SKL/BDG/2026/X/0001",
            Status = "Active",
            ChickInDate = ChickIn,
            Current = days[^1].Cumulative,
            Days = days,
            Standard = Standard
        };
    }
}
