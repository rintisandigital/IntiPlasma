using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.Partnership.Contracts;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Domain.UnitTests.Partnership;

public sealed class ProductionCycleTests
{
    [Fact]
    public void Plan_Should_SnapshotContract_ForPlasmaCycle()
    {
        Farmer farmer = TestData.PlasmaFarmer();
        Coop coop = TestData.Coop(farmer);
        PartnershipContract contract = TestData.ActivePriceContract();

        Result<ProductionCycle> result = ProductionCycle.Plan("SKL/1", coop, farmer, contract, TestData.Today, 8_000, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(CycleStatus.Planned);
        result.Value.BranchId.ShouldBe(coop.BranchId);
        result.Value.ContractSnapshot!.ContractId.ShouldBe(contract.Id);
    }

    [Fact]
    public void Plan_Should_RequireContract_ForPlasmaFarmer()
    {
        Farmer farmer = TestData.PlasmaFarmer();

        ProductionCycle.Plan("SKL/1", TestData.Coop(farmer), farmer, null, TestData.Today, 8_000, null)
            .Error.ShouldBe(CycleErrors.ContractRequired);
    }

    [Fact]
    public void Plan_Should_RejectContract_ForIntiFarmer()
    {
        Farmer farmer = TestData.IntiFarmer();

        ProductionCycle.Plan("SKL/1", TestData.Coop(farmer), farmer, TestData.ActivePriceContract(), TestData.Today, 8_000, null)
            .Error.ShouldBe(CycleErrors.IntiCannotHaveContract);
    }

    [Fact]
    public void Plan_Should_RejectPopulationAboveCapacity()
    {
        Farmer farmer = TestData.PlasmaFarmer();

        ProductionCycle.Plan("SKL/1", TestData.Coop(farmer, capacity: 5_000), farmer, TestData.ActivePriceContract(),
                TestData.Today, 5_001, null)
            .Error.ShouldBe(CycleErrors.InvalidPopulation(5_000));
    }

    [Fact]
    public void Plan_Should_RejectContractOfAnotherBranch()
    {
        Farmer farmer = TestData.PlasmaFarmer();

        ProductionCycle.Plan("SKL/1", TestData.Coop(farmer), farmer, TestData.ActivePriceContract(Guid.NewGuid()),
                TestData.Today, 8_000, null)
            .Error.ShouldBe(ContractErrors.BranchMismatch);
    }

    [Fact]
    public void Plan_Should_RejectContractNotValidOnChickInDate()
    {
        Farmer farmer = TestData.PlasmaFarmer();
        PartnershipContract contract = TestData.ActivePriceContract();
        DateOnly beforeValidity = TestData.Today.AddDays(-7);

        ProductionCycle.Plan("SKL/1", TestData.Coop(farmer), farmer, contract, beforeValidity, 8_000, null)
            .Error.ShouldBe(ContractErrors.NotUsable(contract.Id, beforeValidity));
    }

    [Fact]
    public void Start_Should_ActivatePlannedCycle_AndCancel_ShouldThenFail()
    {
        Farmer farmer = TestData.IntiFarmer();
        ProductionCycle cycle = ProductionCycle.Plan("SKL/1", TestData.Coop(farmer), farmer, null, TestData.Today, 8_000, null).Value;

        cycle.Start(TestData.Today, 7_950).IsSuccess.ShouldBeTrue();

        cycle.Status.ShouldBe(CycleStatus.Active);
        cycle.InitialPopulation.ShouldBe(7_950);
        cycle.Cancel("salah input").Error.ShouldBe(CycleErrors.InvalidTransition(CycleStatus.Active, CycleStatus.Cancelled));
    }
}
