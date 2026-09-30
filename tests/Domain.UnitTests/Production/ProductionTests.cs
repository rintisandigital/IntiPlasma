using Domain.Inventory.StockReturns;
using Domain.MasterData.Farmers;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Cycles;
using Domain.Production.DailyRecordings;

namespace Domain.UnitTests.Production;

public sealed class ProductionTests
{
    private static readonly DateOnly ChickIn = new(2026, 10, 1);

    private static ProductionCycle ActiveCycle(int population = 10_000)
    {
        Farmer farmer = TestData.IntiFarmer();
        ProductionCycle cycle = ProductionCycle.Plan("SKL/1", TestData.Coop(farmer), farmer, null, ChickIn, population, null).Value;
        cycle.Start(ChickIn, population);

        return cycle;
    }

    private static DailyRecordingValues Values(int mortality, int culling = 0, decimal? bw = null) =>
        new(mortality, culling, bw, null, [new DailyRecordingUsageInput(Guid.NewGuid(), Guid.NewGuid(), 10m, 500m)]);

    [Fact]
    public void Performance_Should_MatchTheIndustryFormulas()
    {
        // 10.000 DOC, 400 mati, 100 culling, 9.500 ekor dipanen 19.950 kg (BW 2,1 kg) umur 35 hari, pakan 31.920 kg.
        var performance = CyclePerformance.Calculate(10_000, 400, 100, 9_500, 19_950m, 31_920m, 2.1m, 35m);

        performance.Population.ShouldBe(0);
        performance.DepletionPercent.ShouldBe(5m);
        performance.Fcr.ShouldBe(1.6m);
        performance.Ip.ShouldBe(356.25m); // (95 × 2,1) / (1,6 × 35) × 100
        performance.AdgGram.ShouldBe(60m);
    }

    [Fact]
    public void Depletion_Should_NotExceedPopulation()
    {
        ProductionCycle cycle = ActiveCycle(population: 100);

        cycle.ApplyDepletion(60, 30).IsSuccess.ShouldBeTrue();
        cycle.ApplyDepletion(11, 0).Error.ShouldBe(CycleErrors.PopulationExceeded(10));
        cycle.CurrentPopulation.ShouldBe(10);

        // A revision lowering the numbers gives the population back.
        cycle.ApplyDepletion(-10, 0).IsSuccess.ShouldBeTrue();
        cycle.CurrentPopulation.ShouldBe(20);
    }

    [Fact]
    public void Harvest_Should_MoveToHarvesting_AndCloseRequiresEmptyPopulation()
    {
        ProductionCycle cycle = ActiveCycle(population: 100);
        cycle.ApplyDepletion(5, 0);

        cycle.RecordHarvest(ChickIn.AddDays(34), 60, 126m, "Truk 1").IsSuccess.ShouldBeTrue();
        cycle.Status.ShouldBe(CycleStatus.Harvesting);
        cycle.Harvests.Single().AgeDays.ShouldBe(34);

        var performance = CyclePerformance.Calculate(100, 5, 0, 60, 126m, 300m, 2.1m, 34m);
        cycle.Close(performance).Error.ShouldBe(CycleErrors.PopulationRemaining(35));

        cycle.RecordHarvest(ChickIn.AddDays(35), 36, 1m, null).Error.ShouldBe(CycleErrors.PopulationExceeded(35));
        cycle.RecordHarvest(ChickIn.AddDays(35), 35, 75m, "Truk 2");

        cycle.Close(performance).IsSuccess.ShouldBeTrue();
        cycle.Status.ShouldBe(CycleStatus.Closed);
        cycle.ClosedDate.ShouldBe(ChickIn.AddDays(35));
        cycle.ApplyDepletion(1, 0).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Recording_Should_NotBeBeforeChickIn_OrOnPlannedCycle()
    {
        Farmer farmer = TestData.IntiFarmer();
        ProductionCycle planned = ProductionCycle.Plan("SKL/1", TestData.Coop(farmer), farmer, null, ChickIn, 100, null).Value;

        DailyRecording.Create(null, planned, ChickIn, Values(1)).Error.Code.ShouldBe("Cycles.NotRecordable");
        DailyRecording.Create(null, ActiveCycle(), ChickIn.AddDays(-1), Values(1)).Error.ShouldBe(CycleErrors.BeforeChickIn(ChickIn));
    }

    [Fact]
    public void Revision_Should_KeepPreviousValuesAndReason()
    {
        DailyRecording recording = DailyRecording.Create(Guid.NewGuid(), ActiveCycle(), ChickIn.AddDays(7), Values(12, 1, 180m)).Value;

        recording.Revise(Values(15, 1, 185m), "salah hitung kematian", Guid.NewGuid(), DateTime.UtcNow).IsSuccess.ShouldBeTrue();

        recording.Mortality.ShouldBe(15);
        recording.RevisionNumber.ShouldBe(1);
        recording.AgeDays.ShouldBe(7);
        DailyRecordingRevision revision = recording.Revisions.Single();
        revision.Reason.ShouldBe("salah hitung kematian");
        revision.PreviousValues.ShouldContain("\"mortality\":12");
    }

    [Fact]
    public void StockReturn_Should_GoFromCoopToCentralOnly()
    {
        var branchId = Guid.NewGuid();
        var central = Warehouse.CreateCentral("GI", "Gudang Induk", branchId, null);
        var coopA = Warehouse.CreateForCoop("GK-A", "Kandang A", branchId, Guid.NewGuid(), null);
        var coopB = Warehouse.CreateForCoop("GK-B", "Kandang B", branchId, Guid.NewGuid(), null);
        StockReturnLineInput[] lines = [new(Guid.NewGuid(), Guid.NewGuid(), 5m, 250m)];

        StockReturn.Create("RTR/1", coopA, coopB, Guid.NewGuid(), ChickIn, "mutasi", null, lines)
            .Error.ShouldBe(StockReturnErrors.DestinationMustBeCentral);

        StockReturn.Create("RTR/1", central, coopA, Guid.NewGuid(), ChickIn, "x", null, lines)
            .Error.ShouldBe(StockReturnErrors.SourceMustBeCoop);

        StockReturn.Create("RTR/1", coopA, central, Guid.NewGuid(), ChickIn, "sisa pakan", null, lines).IsSuccess.ShouldBeTrue();
    }
}
