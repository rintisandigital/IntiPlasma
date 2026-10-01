using Domain.Costing.PlasmaSettlements;
using Domain.Finance.Payables;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.MasterData.Items;
using Domain.MasterData.TaxCodes;
using Domain.Partnership.Contracts;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Domain.UnitTests.Costing;

public sealed class SettlementTests
{
    private static readonly DateOnly ChickIn = TestData.Today;
    private static readonly DateOnly Settled = ChickIn.AddDays(40);

    // 1.000 DOC, 50 dead; truck 1: 500 birds / 1.000 kg (BW 2,0 → Rp 19.000), truck 2: 450 birds / 765 kg (BW 1,7 → Rp 19.500).
    private static readonly CyclePerformance Performance = CyclePerformance.Calculate(1_000, 50, 0, 950, 1_765m, 2_400m, 1.858m, 33m);

    private static ProductionCycle ClosedPlasmaCycle(ContractScheme scheme = ContractScheme.PriceContract)
    {
        Farmer farmer = TestData.PlasmaFarmer();
        Coop coop = TestData.Coop(farmer);
        PartnershipContract contract = PartnershipContract.Create("KTR-001", TestData.BranchId, scheme,
            TestData.Terms(profitShare: scheme == ContractScheme.ProfitSharing ? 40m : null)).Value;
        contract.Activate();

        ProductionCycle cycle = ProductionCycle.Plan("SKL/1", coop, farmer, contract, ChickIn, 1_000, null).Value;
        cycle.Start(ChickIn, 1_000);
        cycle.ApplyDepletion(50, 0);
        cycle.RecordHarvest(ChickIn.AddDays(33), 500, 1_000m, null);
        cycle.RecordHarvest(ChickIn.AddDays(34), 450, 765m, null);
        cycle.Close(Performance, CycleCostSummary.Calculate(5_000_000m, 20_000_000m, 500_000m, 950, 1_765m, 24_000_000m));

        return cycle;
    }

    private static SettlementInput Input(ProductionCycle cycle, decimal feedKg = 2_400m, decimal netSales = 0m, decimal cycleCost = 0m) =>
        new(
            cycle.ContractSnapshot!,
            Performance,
            [.. cycle.Harvests.Select(h => new HarvestFigures(h.Date, h.Birds, h.WeightKg))],
            [new ConsumedInput(TestData.FeedItemId, "PKN", ItemCategory.Feed, feedKg, new Money(feedKg * 8_000m))],
            new Money(netSales),
            new Money(cycleCost));

    private static TaxCode Pph(decimal rate)
    {
        var pph = TaxCode.CreateIncomeTax("PPH21", "PPh 21 plasma", IncomeTaxArticle.Pph21);
        pph.SetRates([(new DateOnly(2025, 1, 1), rate, 1m)]);

        return pph;
    }

    [Fact]
    public void PriceContract_Should_PayHarvestsAtGuaranteedPrice_MinusSapronak_PlusBonus()
    {
        ProductionCycle cycle = ClosedPlasmaCycle();

        PlasmaSettlement settlement = PlasmaSettlement.Create(
            "STL/1", cycle, Settled, Input(cycle), Pph(2m), new Money(1_000_000m), null).Value;

        // 1.000 kg × 19.000 + 765 kg × 19.500 − 2.400 kg × 8.500 + bonus FCR 1,36 (≤ 1,45) 1.765 kg × 200
        settlement.Lines.Select(l => l.Amount.Amount).ShouldBe([19_000_000m, 14_917_500m, -20_400_000m, 353_000m]);
        settlement.GrossIncome.ShouldBe(new Money(13_870_500m));
        settlement.IncomeTaxAmount.ShouldBe(new Money(277_410m));
        settlement.NetPayable.ShouldBe(new Money(12_593_090m));
        settlement.Deficit.ShouldBe(Money.Zero);
        settlement.Status.ShouldBe(PlasmaSettlementStatus.Draft);
    }

    [Fact]
    public void Loss_Should_BecomeTheFarmersDebt_WithoutTaxOrPayment()
    {
        ProductionCycle cycle = ClosedPlasmaCycle();

        // 5.000 kg feed × 8.500 = 42.500.000 > 33.917.500 + 353.000
        PlasmaSettlement.Create("STL/1", cycle, Settled, Input(cycle, feedKg: 5_000m), Pph(2m), new Money(1m), null)
            .Error.ShouldBe(PlasmaSettlementErrors.InvalidDebtDeduction(Money.Zero));

        PlasmaSettlement settlement = PlasmaSettlement.Create(
            "STL/1", cycle, Settled, Input(cycle, feedKg: 5_000m), Pph(2m), Money.Zero, null).Value;

        settlement.GrossIncome.ShouldBe(new Money(-8_229_500m));
        settlement.Deficit.ShouldBe(new Money(8_229_500m));
        settlement.IncomeTaxAmount.ShouldBe(Money.Zero);
        settlement.NetPayable.ShouldBe(Money.Zero);

        settlement.Approve(Guid.NewGuid(), DateTime.UtcNow, cycle).IsSuccess.ShouldBeTrue();
        settlement.Status.ShouldBe(PlasmaSettlementStatus.Paid);
        settlement.IsPayable.ShouldBeFalse();
        cycle.Status.ShouldBe(CycleStatus.Settled);
    }

    [Fact]
    public void ProfitSharing_Should_ShareTheProfit_AndNotTheLoss()
    {
        ProductionCycle cycle = ClosedPlasmaCycle(ContractScheme.ProfitSharing);

        // 40% × (35.000.000 − 25.000.000) + bonus 353.000
        PlasmaSettlement profit = PlasmaSettlement.Create(
            "STL/1", cycle, Settled, Input(cycle, netSales: 35_000_000m, cycleCost: 25_000_000m), null, Money.Zero, null).Value;

        profit.Lines.First().Amount.ShouldBe(new Money(4_000_000m));
        profit.GrossIncome.ShouldBe(new Money(4_353_000m));

        PlasmaSettlement loss = PlasmaSettlement.Create(
            "STL/2", cycle, Settled, Input(cycle, netSales: 20_000_000m, cycleCost: 25_000_000m), null, Money.Zero, null).Value;

        loss.Lines.First().Amount.ShouldBe(Money.Zero);
        loss.GrossIncome.ShouldBe(new Money(353_000m));
    }

    [Fact]
    public void Settlement_Should_RequireContractPrices_AndAClosedPlasmaCycle()
    {
        ProductionCycle cycle = ClosedPlasmaCycle();
        SettlementInput input = Input(cycle);

        PlasmaSettlement.Create("STL/1", cycle, Settled,
                input with { Inputs = [.. input.Inputs, new ConsumedInput(Guid.NewGuid(), "DOC", ItemCategory.Doc, 1_000m, new Money(7_500_000m))] },
                null, Money.Zero, null)
            .Error.ShouldBe(PlasmaSettlementErrors.NoInputPrice("DOC"));

        PlasmaSettlement.Create("STL/1", cycle, Settled,
                input with { Harvests = [new HarvestFigures(ChickIn.AddDays(30), 100, 260m)] }, null, Money.Zero, null)
            .Error.ShouldBe(PlasmaSettlementErrors.NoLiveBirdPrice(2.6m));

        PlasmaSettlement.Create("STL/1", cycle, cycle.ClosedDate!.Value.AddDays(-1), input, null, Money.Zero, null)
            .Error.Code.ShouldBe("PlasmaSettlements.BeforeClosing");

        Farmer intiFarmer = TestData.IntiFarmer();
        ProductionCycle inti = ProductionCycle.Plan("SKL/2", TestData.Coop(intiFarmer), intiFarmer, null, ChickIn, 100, null).Value;
        PlasmaSettlement.Create("STL/1", inti, Settled, input, null, Money.Zero, null)
            .Error.Code.ShouldBe("PlasmaSettlements.CycleNotClosed");
    }

    [Fact]
    public void ApprovedSettlement_Should_BePaidThroughAFarmerPaymentVoucher()
    {
        ProductionCycle cycle = ClosedPlasmaCycle();
        PlasmaSettlement settlement = PlasmaSettlement.Create("STL/1", cycle, Settled, Input(cycle), null, Money.Zero, null).Value;

        PaymentVoucher.Create("PV/1", cycle.BranchId, PayeeType.Farmer, cycle.FarmerId, Guid.NewGuid(), Settled, null, null,
                [(settlement, new Money(1m))])
            .Error.ShouldBe(PaymentVoucherErrors.DocumentNotPayable(settlement.Id));

        settlement.Approve(Guid.NewGuid(), DateTime.UtcNow, cycle);
        settlement.Recalculate(cycle, Settled, Input(cycle), null, Money.Zero, null).Error.Code.ShouldBe("PlasmaSettlements.InvalidTransition");

        PaymentVoucher.Create("PV/1", cycle.BranchId, PayeeType.Farmer, Guid.NewGuid(), Guid.NewGuid(), Settled, null, null,
                [(settlement, new Money(1m))])
            .Error.ShouldBe(PaymentVoucherErrors.InvoiceMismatch(settlement.Id));

        PaymentVoucher voucher = PaymentVoucher.Create("PV/1", cycle.BranchId, PayeeType.Farmer, cycle.FarmerId, Guid.NewGuid(), Settled,
            null, null, [(settlement, settlement.NetPayable)]).Value;

        voucher.FarmerId.ShouldBe(cycle.FarmerId);
        voucher.SettlementAllocations.ShouldHaveSingleItem();
        voucher.Approve(Guid.NewGuid(), DateTime.UtcNow);
        voucher.Pay(Settled, [settlement], null, DateTime.UtcNow).IsSuccess.ShouldBeTrue();

        settlement.Status.ShouldBe(PlasmaSettlementStatus.Paid);
        settlement.Outstanding.ShouldBe(Money.Zero);
    }

    [Fact]
    public void CycleCostSummary_Should_CompareFinalCostWithTheRecognizedEstimate()
    {
        var cost = CycleCostSummary.Calculate(7_500_000m, 20_400_000m, 300_000m, 950, 1_765m, 27_000_000m);

        cost.TotalCost.ShouldBe(28_200_000m);
        cost.CostPerKg.ShouldBe(15_977.34m);
        cost.CostPerBird.ShouldBe(29_684.21m);
        cost.Adjustment.ShouldBe(1_200_000m);
    }
}
