using Domain.Common;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.Partnership.Contracts;
using SharedKernel;

namespace Domain.UnitTests;

internal static class TestData
{
    public static readonly Guid BranchId = Guid.NewGuid();
    public static readonly Guid FeedItemId = Guid.NewGuid();
    public static readonly DateOnly Today = new(2026, 10, 1);

    /// <summary>
    /// An approved (active) plasma farmer.
    /// </summary>
    public static Farmer PlasmaFarmer(Guid? branchId = null) => Approved(DraftPlasmaFarmer(branchId));

    public static Farmer DraftPlasmaFarmer(Guid? branchId = null) =>
        Farmer.Create("PLM-001", "Budi Santoso", FarmerType.Plasma, branchId ?? BranchId, "3201010101010001",
            TaxIdentity.None, null, null, BankAccount.None).Value;

    public static Farmer IntiFarmer() =>
        Approved(Farmer.Create("INT-001", "Farm Inti Cianjur", FarmerType.Inti, BranchId, null,
            TaxIdentity.None, null, null, BankAccount.None).Value);

    /// <summary>
    /// An approved (active) coop of an approved farmer.
    /// </summary>
    public static Coop Coop(Farmer farmer, int capacity = 10_000)
    {
        Coop coop = DraftCoop(farmer, capacity);
        coop.Submit(farmer);
        coop.Approve(farmer);

        return coop;
    }

    public static Coop DraftCoop(Farmer farmer, int capacity = 10_000) =>
        Domain.MasterData.Coops.Coop.Create(farmer, "KDG-001", "Kandang 1", capacity, HouseType.ClosedHouse, null, null, null).Value;

    public static ContractTerms Terms(
        decimal? profitShare = null,
        IReadOnlyList<ContractTerms.LiveBirdPrice>? liveBirdPrices = null) =>
        new(
            "Kontrak Harga 2026",
            Today,
            null,
            profitShare,
            null,
            null,
            [new ContractTerms.InputPrice(FeedItemId, new Money(8_500m))],
            liveBirdPrices ??
            [
                new ContractTerms.LiveBirdPrice(0m, 1.8m, new Money(19_500m)),
                new ContractTerms.LiveBirdPrice(1.8m, 2.5m, new Money(19_000m))
            ],
            [
                new ContractTerms.Incentive(
                    "Bonus FCR", IncentiveKind.Bonus, IncentiveMetric.Fcr, 0m, 1.45m, new Money(200m), IncentiveBasis.PerKg)
            ]);

    public static PartnershipContract ActivePriceContract(Guid? branchId = null)
    {
        PartnershipContract contract = PartnershipContract
            .Create("KTR-001", branchId ?? BranchId, ContractScheme.PriceContract, Terms()).Value;

        contract.Submit();
        contract.Approve();

        return contract;
    }

    private static Farmer Approved(Farmer farmer)
    {
        farmer.Submit();
        farmer.Approve();

        return farmer;
    }
}
