using Domain.Finance.Receivables;
using Domain.MasterData.TaxCodes;
using Domain.Sales.CreditNotes;
using Domain.Sales.DeliveryOrders;
using Domain.Sales.SalesInvoices;
using Domain.Sales.SalesOrders;
using SharedKernel;

namespace Domain.UnitTests.Sales;

public sealed class SalesTests
{
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid LiveBird = Guid.NewGuid();
    private static readonly DateOnly OrderDate = new(2026, 10, 1);

    /// <summary>
    /// 2.000 birds, estimated 4.000 kg @ Rp 20.000/kg = Rp 80.000.000.
    /// </summary>
    private static SalesOrder DraftOrder(Guid? taxCodeId = null) =>
        SalesOrder.Create("SO/1", BranchId, CustomerId, OrderDate, null, null,
            [new SalesOrderLineInput(LiveBird, 2_000, 4_000m, new Money(20_000m), taxCodeId)]).Value;

    private static SalesOrder ApprovedOrder(Guid? taxCodeId = null)
    {
        SalesOrder order = DraftOrder(taxCodeId);
        order.Approve(Guid.NewGuid(), DateTime.UtcNow, new CreditCheck(new Money(100_000_000m), Money.Zero), null);

        return order;
    }

    private static HarvestToDeliver Harvest(int birds, decimal kg, Guid? branchId = null, DateOnly? date = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), branchId ?? BranchId, date ?? OrderDate, birds, kg);

    private static DeliveryOrder Deliver(SalesOrder order, params HarvestToDeliver[] harvests)
    {
        DeliveryOrder delivery = DeliveryOrder.Create(
            "DO/1", order, OrderDate, "D 1234 AB", null, null,
            [.. harvests.Select(h => new DeliveryOrderLineInput(1, h))]).Value;

        foreach (DeliveryOrderLine line in delivery.Lines)
        {
            order.RegisterDelivery(line.SalesOrderLineNumber, line.Birds, line.WeightKg);
        }

        return delivery;
    }

    private static TaxCode Ppn12Dpp1112()
    {
        var taxCode = TaxCode.CreateVat("PPN12", "PPN 12% DPP nilai lain", VatTreatment.Taxable);
        taxCode.SetRates([(new DateOnly(2025, 1, 1), 12m, 11m / 12m)]);

        return taxCode;
    }

    [Fact]
    public void Approve_Should_Succeed_WithinCreditLimit()
    {
        SalesOrder order = DraftOrder();

        Result result = order.Approve(Guid.NewGuid(), DateTime.UtcNow, new CreditCheck(new Money(100_000_000m), new Money(20_000_000m)), null);

        result.IsSuccess.ShouldBeTrue();
        order.Status.ShouldBe(SalesOrderStatus.Approved);
        order.CreditOverrideReason.ShouldBeNull();
    }

    [Fact]
    public void Approve_Should_Fail_WhenCreditLimitExceeded_AndSucceedWithOverride()
    {
        SalesOrder order = DraftOrder();
        var check = new CreditCheck(new Money(100_000_000m), new Money(20_000_001m));

        order.Approve(Guid.NewGuid(), DateTime.UtcNow, check, null).Error
            .ShouldBe(SalesOrderErrors.CreditLimitExceeded(check.Limit, check.Exposure, new Money(80_000_000m)));
        order.Status.ShouldBe(SalesOrderStatus.Draft);

        order.Approve(Guid.NewGuid(), DateTime.UtcNow, check, " jaminan giro ").IsSuccess.ShouldBeTrue();
        order.CreditOverrideReason.ShouldBe("jaminan giro");
    }

    [Fact]
    public void Approve_Should_RequireOverride_WhenCustomerHasNoCredit()
    {
        SalesOrder order = DraftOrder();

        order.Approve(Guid.NewGuid(), DateTime.UtcNow, new CreditCheck(Money.Zero, Money.Zero), null)
            .Error.Code.ShouldBe("SalesOrders.CreditLimitExceeded");
    }

    [Fact]
    public void Delivery_Should_TrackBirds_AndRejectOverDelivery()
    {
        SalesOrder order = ApprovedOrder();

        order.RegisterDelivery(1, 1_200, 2_450m).IsSuccess.ShouldBeTrue();
        order.Status.ShouldBe(SalesOrderStatus.PartiallyDelivered);
        order.OutstandingEstimatedAmount.ShouldBe(new Money(32_000_000m));

        order.RegisterDelivery(1, 801, 1_600m).Error.ShouldBe(SalesOrderErrors.OverDelivery(1, 800));

        order.RegisterDelivery(1, 800, 1_590m);
        order.Status.ShouldBe(SalesOrderStatus.Delivered);
        order.Lines.Single().DeliveredWeightKg.ShouldBe(4_040m);

        order.ReverseDelivery(1, 800, 1_590m).IsSuccess.ShouldBeTrue();
        order.Status.ShouldBe(SalesOrderStatus.PartiallyDelivered);
    }

    [Fact]
    public void SalesOrder_Should_NotBeCancelled_AfterDelivery_ButCanBeClosed()
    {
        SalesOrder order = ApprovedOrder();
        order.RegisterDelivery(1, 1_000, 2_000m);

        order.Cancel("batal").Error.ShouldBe(
            SalesOrderErrors.InvalidTransition(SalesOrderStatus.PartiallyDelivered, SalesOrderStatus.Cancelled));

        order.Close().IsSuccess.ShouldBeTrue();
        order.OutstandingEstimatedAmount.ShouldBe(Money.Zero);
        order.RegisterDelivery(1, 1, 2m).Error.ShouldBe(SalesOrderErrors.NotDeliverable(order.Id));
    }

    [Fact]
    public void DeliveryOrder_Should_TakePriceFromOrder_AndValidateHarvests()
    {
        SalesOrder order = ApprovedOrder();

        DeliveryOrder.Create("DO/1", DraftOrder(), OrderDate, null, null, null, [new DeliveryOrderLineInput(1, Harvest(10, 20m))])
            .Error.Code.ShouldBe("SalesOrders.NotDeliverable");

        DeliveryOrder.Create("DO/1", order, OrderDate, null, null, null, [new DeliveryOrderLineInput(1, Harvest(10, 20m, Guid.NewGuid()))])
            .Error.ShouldBe(DeliveryOrderErrors.HarvestBranchMismatch);

        DeliveryOrder.Create("DO/1", order, OrderDate, null, null, null,
                [new DeliveryOrderLineInput(1, Harvest(10, 20m, date: OrderDate.AddDays(1)))])
            .Error.ShouldBe(DeliveryOrderErrors.BeforeHarvestDate(OrderDate.AddDays(1)));

        DeliveryOrder.Create("DO/1", order, OrderDate, null, null, null,
                [new DeliveryOrderLineInput(1, Harvest(1_500, 3_000m)), new DeliveryOrderLineInput(1, Harvest(501, 1_000m))])
            .Error.ShouldBe(SalesOrderErrors.OverDelivery(1, 2_000));

        HarvestToDeliver harvest = Harvest(1_000, 2_050.5m);
        DeliveryOrder.Create("DO/1", order, OrderDate, null, null, null,
                [new DeliveryOrderLineInput(1, harvest), new DeliveryOrderLineInput(1, harvest)])
            .Error.ShouldBe(DeliveryOrderErrors.DuplicateHarvest);

        DeliveryOrder delivery = DeliveryOrder.Create("DO/1", order, OrderDate, null, null, null, [new DeliveryOrderLineInput(1, harvest)]).Value;

        delivery.Status.ShouldBe(DeliveryOrderStatus.Delivered);
        delivery.Amount.ShouldBe(new Money(41_010_000m));
        order.Lines.Single().DeliveredBirds.ShouldBe(0); // the caller registers the delivery
    }

    [Fact]
    public void CancelledDelivery_Should_MarkLinesCancelled()
    {
        DeliveryOrder delivery = Deliver(ApprovedOrder(), Harvest(1_000, 2_000m));

        delivery.Cancel("salah timbang").IsSuccess.ShouldBeTrue();

        delivery.Status.ShouldBe(DeliveryOrderStatus.Cancelled);
        delivery.Lines.ShouldAllBe(l => l.IsCancelled);
        delivery.Cancel("lagi").Error.Code.ShouldBe("DeliveryOrders.InvalidTransition");
    }

    [Fact]
    public void Invoice_Should_CalculateVat_WithTaxBaseRatio()
    {
        TaxCode ppn = Ppn12Dpp1112();
        DeliveryOrder delivery = Deliver(ApprovedOrder(ppn.Id), Harvest(1_000, 2_000m));

        SalesInvoice invoice = SalesInvoice.CreateDraft(
            [delivery], OrderDate.AddDays(1), 14, null, new Dictionary<Guid, TaxCode> { [ppn.Id] = ppn }).Value;

        // DPP 40.000.000 × 11/12 = 36.666.666,67; PPN 12% = 4.400.000,00
        invoice.Subtotal.ShouldBe(new Money(40_000_000m));
        invoice.Lines.Single().VatTaxBase.ShouldBe(new Money(36_666_666.67m));
        invoice.VatAmount.ShouldBe(new Money(4_400_000m));
        invoice.Total.ShouldBe(new Money(44_400_000m));
        invoice.DueDate.ShouldBe(OrderDate.AddDays(15));
        invoice.Number.ShouldBeNull();
        delivery.Status.ShouldBe(DeliveryOrderStatus.Invoiced);
        delivery.SalesInvoiceId.ShouldBe(invoice.Id);
    }

    [Fact]
    public void Invoice_Should_ChargeNoVat_WhenExempt()
    {
        var exempt = TaxCode.CreateVat("PPN-BBS", "PPN dibebaskan", VatTreatment.Exempt);
        DeliveryOrder delivery = Deliver(ApprovedOrder(exempt.Id), Harvest(1_000, 2_000m));

        SalesInvoice invoice = SalesInvoice.CreateDraft(
            [delivery], OrderDate, 0, null, new Dictionary<Guid, TaxCode> { [exempt.Id] = exempt }).Value;

        invoice.VatAmount.ShouldBe(Money.Zero);
        invoice.Total.ShouldBe(new Money(40_000_000m));
        invoice.Lines.Single().TaxCodeId.ShouldBe(exempt.Id);
    }

    [Fact]
    public void Invoice_Should_Fail_WhenVatRateNotInEffect()
    {
        TaxCode ppn = Ppn12Dpp1112();
        DeliveryOrder delivery = Deliver(ApprovedOrder(ppn.Id), Harvest(1_000, 2_000m));
        var taxCodes = new Dictionary<Guid, TaxCode> { [ppn.Id] = ppn };

        ppn.SetRates([(new DateOnly(2027, 1, 1), 12m, 1m)]);

        SalesInvoice.CreateDraft([delivery], OrderDate, 0, null, taxCodes).Error
            .ShouldBe(TaxCodeErrors.NoRate("PPN12", OrderDate));
        delivery.Status.ShouldBe(DeliveryOrderStatus.Delivered);
    }

    [Fact]
    public void Invoice_Should_RejectDeliveriesOfOtherCustomers_AndInvoicedDeliveries()
    {
        DeliveryOrder first = Deliver(ApprovedOrder(), Harvest(500, 1_000m));
        SalesOrder otherCustomerOrder = SalesOrder.Create("SO/2", BranchId, Guid.NewGuid(), OrderDate, null, null,
            [new SalesOrderLineInput(LiveBird, 100, 200m, new Money(20_000m), null)]).Value;
        otherCustomerOrder.Approve(Guid.NewGuid(), DateTime.UtcNow, new CreditCheck(new Money(1_000_000_000m), Money.Zero), null);
        DeliveryOrder other = Deliver(otherCustomerOrder, Harvest(100, 200m));

        SalesInvoice.CreateDraft([first, other], OrderDate, 0, null, new Dictionary<Guid, TaxCode>())
            .Error.ShouldBe(SalesInvoiceErrors.MixedDeliveries);

        SalesInvoice.CreateDraft([first], OrderDate, 0, null, new Dictionary<Guid, TaxCode>()).IsSuccess.ShouldBeTrue();
        SalesInvoice.CreateDraft([first], OrderDate, 0, null, new Dictionary<Guid, TaxCode>())
            .Error.ShouldBe(DeliveryOrderErrors.NotInvoiceable(first.Id));
    }

    [Fact]
    public void CancelledDraft_Should_ReleaseDeliveries_AndPostedInvoiceShouldBeFinal()
    {
        DeliveryOrder delivery = Deliver(ApprovedOrder(), Harvest(1_000, 2_000m));
        SalesInvoice draft = SalesInvoice.CreateDraft([delivery], OrderDate, 0, null, new Dictionary<Guid, TaxCode>()).Value;

        draft.Cancel("salah harga", [delivery]).IsSuccess.ShouldBeTrue();
        delivery.Status.ShouldBe(DeliveryOrderStatus.Delivered);
        delivery.SalesInvoiceId.ShouldBeNull();

        SalesInvoice invoice = SalesInvoice.CreateDraft([delivery], OrderDate, 0, null, new Dictionary<Guid, TaxCode>()).Value;
        invoice.Post("INV/1", Guid.NewGuid(), DateTime.UtcNow).IsSuccess.ShouldBeTrue();

        invoice.Number.ShouldBe("INV/1");
        invoice.DomainEvents.OfType<SalesInvoicePostedDomainEvent>().ShouldHaveSingleItem();
        invoice.Cancel("batal", [delivery]).Error.Code.ShouldBe("SalesInvoices.InvalidTransition");
        invoice.Post("INV/2", null, DateTime.UtcNow).Error.Code.ShouldBe("SalesInvoices.InvalidTransition");
    }

    [Fact]
    public void Payments_Should_BePartial_AndNotExceedOutstanding()
    {
        DeliveryOrder delivery = Deliver(ApprovedOrder(), Harvest(1_000, 2_000m));
        SalesInvoice invoice = SalesInvoice.CreateDraft([delivery], OrderDate, 0, null, new Dictionary<Guid, TaxCode>()).Value;

        invoice.RegisterPayment(new Money(1m)).Error.ShouldBe(SalesInvoiceErrors.NotPayable(invoice.Id));

        invoice.Post("INV/1", null, DateTime.UtcNow);
        invoice.RegisterPayment(new Money(15_000_000m)).IsSuccess.ShouldBeTrue();
        invoice.Status.ShouldBe(SalesInvoiceStatus.PartiallyPaid);
        invoice.Outstanding.ShouldBe(new Money(25_000_000m));

        invoice.RegisterPayment(new Money(25_000_000.01m)).Error.ShouldBe(SalesInvoiceErrors.OverPayment("INV/1", new Money(25_000_000m)));

        invoice.RegisterPayment(new Money(25_000_000m));
        invoice.Status.ShouldBe(SalesInvoiceStatus.Paid);
    }

    [Fact]
    public void Receipt_Should_ValidateAllocations()
    {
        DeliveryOrder delivery = Deliver(ApprovedOrder(), Harvest(1_000, 2_000m));
        SalesInvoice invoice = SalesInvoice.CreateDraft([delivery], OrderDate, 0, null, new Dictionary<Guid, TaxCode>()).Value;
        invoice.Post("INV/1", null, DateTime.UtcNow);

        Receipt(Guid.NewGuid(), OrderDate, [(invoice, new Money(1m))])
            .Error.ShouldBe(CustomerReceiptErrors.InvoiceMismatch(invoice.Id));

        Receipt(CustomerId, OrderDate, [(invoice, new Money(40_000_001m))])
            .Error.Code.ShouldBe("SalesInvoices.OverPayment");

        Receipt(CustomerId, OrderDate.AddDays(-1), [(invoice, new Money(1m))])
            .Error.ShouldBe(CustomerReceiptErrors.BeforeInvoiceDate("INV/1"));

        Receipt(CustomerId, OrderDate, []).Error.ShouldBe(CustomerReceiptErrors.NoAllocations);

        CustomerReceipt receipt = Receipt(CustomerId, OrderDate, [(invoice, new Money(10_000_000m))], new Money(5_000_000m)).Value;

        receipt.Amount.ShouldBe(new Money(15_000_000m));
        receipt.UnappliedAdvance.ShouldBe(new Money(5_000_000m));
        receipt.DomainEvents.OfType<CustomerReceiptPostedDomainEvent>().ShouldHaveSingleItem();
        invoice.PaidAmount.ShouldBe(Money.Zero); // the caller registers the payment
    }

    [Fact]
    public void Advance_Should_BeAppliedLater_AndBlockTheVoid()
    {
        SalesInvoice invoice = PostedInvoice();
        CustomerReceipt receipt = Receipt(CustomerId, OrderDate, [], new Money(30_000_000m)).Value;

        receipt.ApplyAdvance(OrderDate, [(invoice, new Money(30_000_001m))]).Error
            .ShouldBe(CustomerReceiptErrors.AdvanceExceeded(new Money(30_000_000m)));

        receipt.ApplyAdvance(OrderDate.AddDays(1), [(invoice, new Money(25_000_000m))]).IsSuccess.ShouldBeTrue();

        invoice.PaidAmount.ShouldBe(new Money(25_000_000m));
        receipt.UnappliedAdvance.ShouldBe(new Money(5_000_000m));
        receipt.DomainEvents.OfType<CustomerAdvanceAppliedDomainEvent>().ShouldHaveSingleItem();
        receipt.Void(OrderDate.AddDays(2), "giro tolak", [invoice]).Error.ShouldBe(CustomerReceiptErrors.AdvanceAlreadyApplied);
    }

    [Fact]
    public void Void_Should_TakeThePaymentsBackFromTheInvoices()
    {
        SalesInvoice invoice = PostedInvoice();
        CustomerReceipt receipt = Receipt(CustomerId, OrderDate, [(invoice, new Money(40_000_000m))]).Value;
        invoice.RegisterPayment(new Money(40_000_000m));
        invoice.Status.ShouldBe(SalesInvoiceStatus.Paid);

        receipt.Void(OrderDate.AddDays(-1), "x", [invoice]).Error.ShouldBe(CustomerReceiptErrors.VoidBeforeReceipt);
        receipt.Void(OrderDate.AddDays(3), "giro tolak", [invoice]).IsSuccess.ShouldBeTrue();

        receipt.Status.ShouldBe(CustomerReceiptStatus.Voided);
        invoice.Status.ShouldBe(SalesInvoiceStatus.Posted);
        invoice.Outstanding.ShouldBe(new Money(40_000_000m));
        receipt.DomainEvents.OfType<CustomerReceiptVoidedDomainEvent>().ShouldHaveSingleItem();
        receipt.Void(OrderDate.AddDays(3), "lagi", [invoice]).Error.ShouldBe(CustomerReceiptErrors.Voided(receipt.Id));
    }

    [Fact]
    public void CreditNote_Should_ReduceTheInvoice_WithProportionalVat()
    {
        var ppn = TaxCode.CreateVat("PPN11", "PPN 11%", VatTreatment.Taxable);
        ppn.SetRates([(new DateOnly(2025, 1, 1), 11m, 1m)]);
        DeliveryOrder delivery = Deliver(ApprovedOrder(ppn.Id), Harvest(1_000, 2_000m));
        SalesInvoice invoice = SalesInvoice.CreateDraft([delivery], OrderDate, 0, null, new Dictionary<Guid, TaxCode> { [ppn.Id] = ppn }).Value;
        invoice.Post("INV/1", null, DateTime.UtcNow);

        // DPP 40.000.000 + PPN 4.400.000; credit 1.000.000 of DPP (e.g. susut timbang) with PPN 110.000.
        SalesCreditNote.Create("CN/1", invoice, OrderDate, "susut", [(1, new Money(40_000_001m))])
            .Error.Code.ShouldBe("SalesCreditNotes.AboveCreditable");

        SalesCreditNote creditNote = SalesCreditNote.Create("CN/1", invoice, OrderDate, "susut", [(1, new Money(1_000_000m))]).Value;

        creditNote.VatAmount.ShouldBe(new Money(110_000m));
        creditNote.Total.ShouldBe(new Money(1_110_000m));

        invoice.ApplyCreditNote([(1, creditNote.Lines.Single().Amount)], creditNote.Total);

        invoice.Outstanding.ShouldBe(new Money(43_290_000m));
        invoice.Lines.Single().CreditableAmount.ShouldBe(new Money(39_000_000m));
    }

    private static SalesInvoice PostedInvoice()
    {
        DeliveryOrder delivery = Deliver(ApprovedOrder(), Harvest(1_000, 2_000m));
        SalesInvoice invoice = SalesInvoice.CreateDraft([delivery], OrderDate, 0, null, new Dictionary<Guid, TaxCode>()).Value;
        invoice.Post("INV/1", null, DateTime.UtcNow);

        return invoice;
    }

    private static Result<CustomerReceipt> Receipt(
        Guid customerId,
        DateOnly date,
        IReadOnlyList<(SalesInvoice Invoice, Money Amount)> allocations,
        Money? advance = null) =>
        CustomerReceipt.Create(
            "RCV/1", BranchId, customerId, date, Guid.NewGuid(), Guid.NewGuid(), "TRF-001", null, allocations, advance ?? Money.Zero);
}
