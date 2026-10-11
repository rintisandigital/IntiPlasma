using Domain.MasterData.BreedStandards;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Domain.UnitTests.MasterData;

public sealed class BreedStandardTests
{
    private static readonly StandardPoint[] Weekly =
    [
        new(0, 42m, null, 0m, null, 0m),
        new(7, 190m, 0.85m, 0.8m, null, 160m),
        new(14, 480m, 1.05m, 1.5m, null, 520m),
    ];

    private static BreedStandard Standard(params StandardPoint[] rows) =>
        BreedStandard.Create("std-1", "CP 707 (contoh)", "CP 707", null, rows.Length == 0 ? Weekly : rows).Value;

    [Fact]
    public void Create_Should_NormalizeCode_AndOrderRows()
    {
        BreedStandard standard = Standard(Weekly[2], Weekly[0], Weekly[1]);

        standard.Code.ShouldBe("STD-1");
        standard.IsActive.ShouldBeTrue();
        standard.Rows.Select(r => r.AgeDays).ShouldBe([0, 7, 14]);
    }

    [Fact]
    public void ReplaceRows_Should_RejectInvalidRows()
    {
        BreedStandard standard = Standard();

        standard.ReplaceRows([]).Error.ShouldBe(BreedStandardErrors.NoRows);
        standard.ReplaceRows([new(71, 100m, null, null, null, null)]).Error.ShouldBe(BreedStandardErrors.InvalidAge(71));
        standard.ReplaceRows([new(7, 190m, null, null, null, null), new(7, 200m, null, null, null, null)])
            .Error.ShouldBe(BreedStandardErrors.DuplicateAge(7));
        standard.ReplaceRows([new(7, 0m, null, null, null, null)]).Error.ShouldBe(BreedStandardErrors.InvalidValue(7));
        standard.ReplaceRows([new(7, 190m, null, null, null, null), new(14, 180m, null, null, null, null)])
            .Error.ShouldBe(BreedStandardErrors.Decreasing(14, "body weight"));
        standard.ReplaceRows([new(0, 42m, null, 1m, null, null), new(7, 190m, null, null, null, null), new(14, 480m, null, 0.5m, null, null)])
            .Error.ShouldBe(BreedStandardErrors.Decreasing(14, "depletion"));

        standard.Rows.Count.ShouldBe(3);
    }

    [Fact]
    public void ReplaceRows_Should_UpdateExistingAges_AndDropMissingOnes()
    {
        BreedStandard standard = Standard();
        BreedStandardRow week1 = standard.Rows.Single(r => r.AgeDays == 7);

        standard.ReplaceRows([new(0, 40m, null, null, null, null), new(7, 200m, null, null, null, null)]).IsSuccess.ShouldBeTrue();

        standard.Rows.Select(r => r.AgeDays).ShouldBe([0, 7]);
        standard.Rows.Single(r => r.AgeDays == 7).ShouldBeSameAs(week1);
        week1.BodyWeightGram.ShouldBe(200m);
    }

    [Fact]
    public void ReplaceRows_Should_DeriveCumulativeFeed_FromCompleteDailyFeed()
    {
        BreedStandard standard = Standard(
            new(0, 42m, null, null, 0m, null), new(1, 55m, null, null, 13m, null), new(2, 70m, null, null, 16m, null));

        standard.Rows.Select(r => r.CumulativeFeedGram).ShouldBe([0m, 13m, 29m]);

        BreedStandard gaps = Standard(new(0, 42m, null, null, 0m, null), new(7, 190m, null, null, 30m, null));

        gaps.Rows.ShouldAllBe(r => r.CumulativeFeedGram == null);
    }

    [Fact]
    public void ValueAt_Should_Interpolate_BetweenRows_AndStopAtTheEnds()
    {
        BreedStandardSnapshot snapshot = Standard().ToSnapshot();

        StandardValue day10 = snapshot.ValueAt(10)!;
        day10.BodyWeightGram.ShouldBe(314.286m); // 190 + (480 − 190) × 3/7
        day10.Fcr.ShouldBe(0.936m);
        day10.DepletionPercent.ShouldBe(1.1m);
        day10.CumulativeFeedGram.ShouldBe(314.286m);

        StandardValue day3 = snapshot.ValueAt(3)!;
        day3.Fcr.ShouldBeNull(); // age 0 has no FCR: nothing to interpolate from
        day3.BodyWeightGram.ShouldBe(105.429m);

        snapshot.ValueAt(14)!.BodyWeightGram.ShouldBe(480m);
        snapshot.ValueAt(15).ShouldBeNull();
        snapshot.ValueAt(-1).ShouldBeNull();
    }

    [Fact]
    public void Deviation_Should_FollowTheFormulas_AndThresholds()
    {
        var standard = new StandardValue(35, 2_000m, 1.5m, 4m, 3_000m);

        var deviation = StandardDeviation.Calculate(standard, 1_880m, 1.56m, 5.2m, 3_150m);

        deviation.BodyWeightPercent.ShouldBe(-6m);
        deviation.Fcr.ShouldBe(0.06m);
        deviation.DepletionPoints.ShouldBe(1.2m);
        deviation.CumulativeFeedPercent.ShouldBe(5m);

        deviation.IsBodyWeightBelow(StandardThresholds.Default).ShouldBeTrue();
        deviation.IsFcrAbove(StandardThresholds.Default).ShouldBeTrue();
        deviation.IsDepletionAbove(StandardThresholds.Default).ShouldBeTrue();

        var onTarget = StandardDeviation.Calculate(standard, 1_950m, 1.52m, 4.5m, null);
        onTarget.CumulativeFeedPercent.ShouldBeNull();
        onTarget.IsBelowStandard(StandardThresholds.Default).ShouldBeFalse();

        StandardDeviation.Calculate(standard, null, null, 4m, null).BodyWeightPercent.ShouldBeNull();
    }

    [Fact]
    public void Default_Should_StayActive()
    {
        BreedStandard standard = Standard();
        standard.MakeDefault().IsSuccess.ShouldBeTrue();

        standard.Update("CP 707", "CP 707", null, isActive: false).Error.ShouldBe(BreedStandardErrors.DefaultMustBeActive);

        standard.ClearDefault();
        standard.Update("CP 707", "CP 707", null, isActive: false).IsSuccess.ShouldBeTrue();
        standard.MakeDefault().Error.ShouldBe(BreedStandardErrors.Inactive("STD-1"));
    }

    [Fact]
    public void Cycle_Should_FreezeTheStandard_WhenClosed_AndLockItWhenSettled()
    {
        Farmer farmer = TestData.IntiFarmer();
        var standardId = Guid.NewGuid();
        ProductionCycle cycle = ProductionCycle.Plan("SKL/1", TestData.Coop(farmer), farmer, null, TestData.Today, 100, null, standardId).Value;
        cycle.BreedStandardId.ShouldBe(standardId);

        cycle.SetBreedStandard(null, Standard().ToSnapshot()).IsSuccess.ShouldBeTrue();
        cycle.BreedStandardId.ShouldBeNull();
        cycle.ClosingStandard.ShouldBeNull(); // only a closed cycle keeps a frozen standard

        cycle.Start(TestData.Today, 100);
        cycle.RecordHarvest(TestData.Today.AddDays(30), 100, 180m, null);
        BreedStandardSnapshot frozen = Standard().ToSnapshot();
        cycle.Close(
            CyclePerformance.Calculate(100, 0, 0, 100, 180m, 300m, 1.8m, 30m),
            CycleCostSummary.Calculate(1m, 1m, 0m, 100, 180m, 2m),
            frozen).IsSuccess.ShouldBeTrue();
        cycle.ClosingStandard.ShouldBe(frozen);

        BreedStandardSnapshot other = Standard(new StandardPoint(0, 40m, null, null, null, null)).ToSnapshot();
        cycle.SetBreedStandard(other.Id, other).IsSuccess.ShouldBeTrue();
        cycle.ClosingStandard.ShouldBe(other);

        cycle.MarkSettled();
        cycle.SetBreedStandard(null, null).Error.ShouldBe(CycleErrors.StandardLocked(CycleStatus.Settled));
    }
}
