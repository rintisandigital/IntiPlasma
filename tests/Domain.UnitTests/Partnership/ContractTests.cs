using Domain.Partnership.Contracts;
using SharedKernel;

namespace Domain.UnitTests.Partnership;

public sealed class ContractTests
{
    [Fact]
    public void Activate_Should_Fail_WhenPriceContractHasNoLiveBirdPrices()
    {
        PartnershipContract contract = PartnershipContract
            .Create("KTR-1", TestData.BranchId, ContractScheme.PriceContract, TestData.Terms(liveBirdPrices: []))
            .Value;

        contract.Activate().Error.ShouldBe(ContractErrors.PriceContractIncomplete);
    }

    [Fact]
    public void Create_Should_Fail_WhenWeightRangesOverlap()
    {
        ContractTerms terms = TestData.Terms(liveBirdPrices:
        [
            new ContractTerms.LiveBirdPrice(0m, 2.0m, new Money(19_500m)),
            new ContractTerms.LiveBirdPrice(1.8m, 2.5m, new Money(19_000m))
        ]);

        PartnershipContract.Create("KTR-1", TestData.BranchId, ContractScheme.PriceContract, terms)
            .Error.ShouldBe(ContractErrors.OverlappingWeightRanges);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    [InlineData(100d)]
    public void Create_Should_RequireValidShare_ForProfitSharing(double? share)
    {
        ContractTerms terms = TestData.Terms(profitShare: (decimal?)share);

        PartnershipContract.Create("KTR-1", TestData.BranchId, ContractScheme.ProfitSharing, terms)
            .Error.ShouldBe(ContractErrors.InvalidProfitShare);
    }

    [Fact]
    public void ActiveContract_Should_BeImmutable()
    {
        PartnershipContract contract = TestData.ActivePriceContract();

        contract.UpdateTerms(TestData.Terms()).Error.ShouldBe(ContractErrors.NotDraft(contract.Id));
        contract.DomainEvents.ShouldContain(e => e is ContractActivatedDomainEvent);
    }

    [Fact]
    public void IsUsableOn_Should_RespectStatusAndValidityPeriod()
    {
        PartnershipContract contract = TestData.ActivePriceContract();

        contract.IsUsableOn(TestData.Today).ShouldBeTrue();
        contract.IsUsableOn(TestData.Today.AddDays(-1)).ShouldBeFalse();

        contract.Deactivate();

        contract.IsUsableOn(TestData.Today).ShouldBeFalse();
    }

    [Fact]
    public void CreateSnapshot_Should_CopyAllTerms()
    {
        PartnershipContract contract = TestData.ActivePriceContract();

        ContractSnapshot snapshot = contract.CreateSnapshot();

        snapshot.ContractId.ShouldBe(contract.Id);
        snapshot.InputPrices.Single().Price.ShouldBe(new Money(8_500m));
        snapshot.LiveBirdPrices.Count.ShouldBe(2);
        snapshot.Incentives.Single().Metric.ShouldBe(IncentiveMetric.Fcr);
    }
}
