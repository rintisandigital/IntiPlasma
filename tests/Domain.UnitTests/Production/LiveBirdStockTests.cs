using Domain.MasterData.Farmers;
using Domain.MasterData.WeightRanges;
using Domain.Partnership.Cycles;
using Domain.Production.LiveBirdStock;

namespace Domain.UnitTests.Production;

/// <summary>
/// Rentang bobot & stok ayam harian (PLAN-MOBILE M-22 s.d. M-27, M-46, M-47).
/// </summary>
public sealed class LiveBirdStockTests
{
    private static readonly DateOnly ChickIn = new(2026, 9, 1);

    private static ProductionCycle ActiveCycle()
    {
        Farmer farmer = TestData.IntiFarmer();
        ProductionCycle cycle = ProductionCycle.Plan("SKL/1", TestData.Coop(farmer), farmer, null, ChickIn, 5_000, null).Value;
        cycle.Start(ChickIn, 5_000);

        return cycle;
    }

    private static WeightRange Range(decimal? min, decimal? max, string code = "R") =>
        WeightRange.Create(code, $"Rentang {code}", min, max, 1).Value;

    [Theory]
    [InlineData(null, null)]
    [InlineData(2.0, 1.8)]
    [InlineData(1.8, 1.8)]
    [InlineData(-0.1, 1.0)]
    public void WeightRange_Should_RejectInvalidBounds(double? min, double? max) =>
        WeightRange.Create("X", "X", (decimal?)min, (decimal?)max, 1).Error.ShouldBe(WeightRangeErrors.InvalidBounds);

    [Fact]
    public void WeightRange_Should_IncludeTheLowerAndExcludeTheUpperBound()
    {
        WeightRange range = Range(1.6m, 1.8m);

        range.Contains(1.6m).ShouldBeTrue();
        range.Contains(1.799m).ShouldBeTrue();
        range.Contains(1.8m).ShouldBeFalse();
        range.Contains(1.599m).ShouldBeFalse();
        Range(null, 1.4m).Contains(0.5m).ShouldBeTrue();
        Range(2.0m, null).Contains(3.5m).ShouldBeTrue();
    }

    [Theory]
    [InlineData(1.6, 1.8, 1.8, 2.0, false)]
    [InlineData(1.6, 1.8, 1.7, 2.0, true)]
    [InlineData(null, 1.4, 1.4, 1.6, false)]
    [InlineData(null, 1.4, 1.2, 1.6, true)]
    [InlineData(2.0, null, 1.8, 2.0, false)]
    [InlineData(2.0, null, 1.8, 2.1, true)]
    public void WeightRange_Should_DetectOverlaps(double? min, double? max, double? otherMin, double? otherMax, bool overlaps)
    {
        WeightRange range = Range((decimal?)min, (decimal?)max);
        WeightRange other = Range((decimal?)otherMin, (decimal?)otherMax, "O");

        range.Overlaps(other).ShouldBe(overlaps);
        other.Overlaps(range).ShouldBe(overlaps);
    }

    [Fact]
    public void Entry_Should_KeepTheCycleData_AndTheAverage()
    {
        ProductionCycle cycle = ActiveCycle();
        WeightRange range = Range(1.6m, 1.8m);
        var id = Guid.CreateVersion7();

        LiveBirdStockEntry entry = LiveBirdStockEntry.Create(id, cycle, ChickIn.AddDays(30), range, 1_000, 1_700m, " Siap panen ").Value;

        entry.Id.ShouldBe(id);
        entry.CycleId.ShouldBe(cycle.Id);
        entry.CoopId.ShouldBe(cycle.CoopId);
        entry.BranchId.ShouldBe(cycle.BranchId);
        entry.AgeDays.ShouldBe(30);
        entry.AverageWeightKg.ShouldBe(1.7m);
        entry.Notes.ShouldBe("Siap panen");
    }

    [Fact]
    public void Entry_Should_RejectAnAverageOutsideTheRange_AndEmptyFigures()
    {
        ProductionCycle cycle = ActiveCycle();
        WeightRange range = Range(1.6m, 1.8m);

        LiveBirdStockEntry.Create(null, cycle, ChickIn.AddDays(30), range, 1_000, 1_900m, null)
            .Error.Code.ShouldBe("LiveBirdStock.AverageOutsideRange");
        LiveBirdStockEntry.Create(null, cycle, ChickIn.AddDays(30), range, 0, 1_700m, null)
            .Error.ShouldBe(LiveBirdStockErrors.InvalidQuantity);
        LiveBirdStockEntry.Create(null, cycle, ChickIn.AddDays(-1), range, 1_000, 1_700m, null)
            .Error.Code.ShouldBe("Cycles.BeforeChickIn");
    }

    [Fact]
    public void Entry_Should_NeedAnActiveRange()
    {
        WeightRange range = Range(1.6m, 1.8m);
        range.Update(range.Name, 1, isActive: false);

        LiveBirdStockEntry.Create(null, ActiveCycle(), ChickIn.AddDays(30), range, 1_000, 1_700m, null)
            .Error.Code.ShouldBe("WeightRanges.Inactive");
    }

    [Fact]
    public void Update_Should_ChangeTheFigures_ButNotTheRange()
    {
        ProductionCycle cycle = ActiveCycle();
        WeightRange range = Range(1.6m, 1.8m);
        LiveBirdStockEntry entry = LiveBirdStockEntry.Create(null, cycle, ChickIn.AddDays(30), range, 1_000, 1_700m, null).Value;

        entry.Update(cycle, range, 900, 1_530m, null).IsSuccess.ShouldBeTrue();
        entry.Birds.ShouldBe(900);
        entry.WeightKg.ShouldBe(1_530m);

        entry.Update(cycle, Range(1.8m, 2.0m, "B"), 900, 1_700m, null).Error.ShouldBe(LiveBirdStockErrors.KeyMismatch);
    }
}
