using Domain.Finance.CashBank;
using Domain.Finance.Payables;
using Domain.Inventory.GoodsReceipts;
using Domain.MasterData.Items;
using Domain.MasterData.TaxCodes;
using Domain.MasterData.Warehouses;
using Domain.Procurement.PurchaseOrders;
using SharedKernel;

namespace Domain.UnitTests.Finance;

public sealed class PayablesAndCashBankTests
{
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid VendorId = Guid.NewGuid();
    private static readonly Guid Feed = Guid.NewGuid();
    private static readonly Guid Sak = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 9, 1);

    private static TaxCode Ppn11()
    {
        var ppn = TaxCode.CreateVat("PPN11", "PPN 11%", VatTreatment.Taxable);
        ppn.SetRates([(new DateOnly(2025, 1, 1), 11m, 1m)]);

        return ppn;
    }

    private static TaxCode Pph23()
    {
        var pph = TaxCode.CreateIncomeTax("PPH23", "PPh 23", IncomeTaxArticle.Pph23);
        pph.SetRates([(new DateOnly(2025, 1, 1), 2m, 1m)]);

        return pph;
    }

    /// <summary>
    /// PO of 100 SAK feed @ Rp 430.000 and a receipt of 30 SAK (Rp 12.900.000).
    /// </summary>
    private static (PurchaseOrder Order, GoodsReceipt Receipt) Received(Guid? taxCodeId = null)
    {
        PurchaseOrder order = PurchaseOrder.Create("PO/1", BranchId, VendorId, Date, null, null,
            [new PurchaseOrderLineInput(Feed, Sak, 100m, new Money(430_000m), taxCodeId)]).Value;
        order.Approve(Guid.NewGuid(), DateTime.UtcNow);

        GoodsReceipt receipt = GoodsReceipt.Create("BPB/1", order, Warehouse.CreateCentral("GI", "Gudang", BranchId, null), null, Date,
            null, null, [new GoodsReceiptLineInput(1, 30m, 1_500m)], new Dictionary<Guid, ItemCategory> { [Feed] = ItemCategory.Feed }).Value;

        return (order, receipt);
    }

    private static Result<VendorInvoice> Invoice(
        (PurchaseOrder Order, GoodsReceipt Receipt) received,
        decimal quantity,
        decimal price,
        IReadOnlyDictionary<Guid, TaxCode>? taxCodes = null,
        Guid? incomeTaxCodeId = null,
        string number = "F-001") =>
        VendorInvoice.CreateDraft(
            BranchId, VendorId, 30, number, null, Date, null, incomeTaxCodeId,
            [new VendorInvoiceLineInput(received.Receipt, 1, received.Order.Lines.Single(), quantity, new Money(price))],
            taxCodes ?? new Dictionary<Guid, TaxCode>());

    [Fact]
    public void VendorInvoice_Should_BillReceivedQuantity_AndClearTheReceiptValueToTheCent()
    {
        (PurchaseOrder, GoodsReceipt) received = Received();

        VendorInvoice first = Invoice(received, 10m, 430_000m).Value;
        first.GoodsValue.ShouldBe(new Money(4_300_000m));
        first.PriceVariance.ShouldBe(Money.Zero);

        Invoice(received, 21m, 430_000m, number: "F-002").Error
            .ShouldBe(GoodsReceiptErrors.OverInvoiced("BPB/1", 1, 20m));

        // The rest is billed Rp 10.000 per SAK above the order price.
        VendorInvoice second = Invoice(received, 20m, 440_000m, number: "F-002").Value;
        second.GoodsValue.ShouldBe(new Money(8_600_000m));
        second.PriceVariance.ShouldBe(new Money(200_000m));
        second.DueDate.ShouldBe(Date.AddDays(30));
        received.Item2.Lines.Single().UninvoicedQuantity.ShouldBe(0m);
        received.Item2.Lines.Single().ValueInvoiced.ShouldBe(new Money(12_900_000m));
    }

    [Fact]
    public void VendorInvoice_Should_NeedApproval_AbovePriceTolerance()
    {
        VendorInvoice invoice = Invoice(Received(), 10m, 440_000m).Value;

        // 10.000 / 430.000 = 2,3256%
        invoice.MaxPriceDeviationPercent.ShouldBe(2.3256m);
        invoice.EnsurePostable(2m, null).Error.ShouldBe(VendorInvoiceErrors.PriceVarianceAboveTolerance(2.3256m, 2m));
        invoice.EnsurePostable(2.5m, null).IsSuccess.ShouldBeTrue();

        invoice.Post("VI/1", 2m, "harga naik per 1 Sept", null, DateTime.UtcNow).IsSuccess.ShouldBeTrue();
        invoice.PriceVarianceApprovalReason.ShouldBe("harga naik per 1 Sept");
        invoice.DomainEvents.OfType<VendorInvoicePostedDomainEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void VendorInvoice_Should_AddInputVat_AndWithholdIncomeTax()
    {
        TaxCode ppn = Ppn11();
        TaxCode pph = Pph23();

        VendorInvoice invoice = Invoice(
            Received(ppn.Id), 20m, 440_000m, new Dictionary<Guid, TaxCode> { [ppn.Id] = ppn, [pph.Id] = pph }, pph.Id).Value;

        invoice.Subtotal.ShouldBe(new Money(8_800_000m));
        invoice.VatAmount.ShouldBe(new Money(968_000m));
        invoice.IncomeTaxAmount.ShouldBe(new Money(176_000m));
        invoice.Total.ShouldBe(new Money(9_592_000m));
    }

    [Fact]
    public void CancelledDraft_Should_ReleaseTheBilledQuantity()
    {
        (PurchaseOrder, GoodsReceipt Receipt) received = Received();
        VendorInvoice invoice = Invoice(received, 30m, 430_000m).Value;

        invoice.Cancel("salah nomor", [received.Receipt]).IsSuccess.ShouldBeTrue();

        received.Receipt.Lines.Single().UninvoicedQuantity.ShouldBe(30m);
        received.Receipt.Lines.Single().ValueInvoiced.ShouldBe(Money.Zero);
        Invoice(received, 30m, 430_000m, number: "F-002").IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void PaymentVoucher_Should_PayOnlyWhenApproved_AndNotAboveOutstanding()
    {
        VendorInvoice invoice = Invoice(Received(), 30m, 430_000m).Value;
        invoice.Post("VI/1", 0m, null, null, DateTime.UtcNow);

        PaymentVoucher.Create("PV/1", BranchId, VendorId, Guid.NewGuid(), Date, null, null, [(invoice, new Money(12_900_001m))])
            .Error.Code.ShouldBe("VendorInvoices.OverPayment");

        PaymentVoucher voucher = PaymentVoucher.Create(
            "PV/1", BranchId, VendorId, Guid.NewGuid(), Date, null, null, [(invoice, new Money(10_000_000m))]).Value;

        voucher.Pay(Date, [invoice], null, DateTime.UtcNow).Error
            .ShouldBe(PaymentVoucherErrors.InvalidTransition(PaymentVoucherStatus.Draft, PaymentVoucherStatus.Paid));

        voucher.Approve(Guid.NewGuid(), DateTime.UtcNow).IsSuccess.ShouldBeTrue();
        voucher.Pay(Date.AddDays(5), [invoice], null, DateTime.UtcNow).IsSuccess.ShouldBeTrue();

        voucher.PaymentDate.ShouldBe(Date.AddDays(5));
        invoice.Status.ShouldBe(VendorInvoiceStatus.PartiallyPaid);
        invoice.Outstanding.ShouldBe(new Money(2_900_000m));
        voucher.DomainEvents.OfType<PaymentVoucherPaidDomainEvent>().ShouldHaveSingleItem();
        voucher.Cancel("x").Error.Code.ShouldBe("PaymentVouchers.InvalidTransition");
    }

    [Fact]
    public void CashOut_Should_NeedApproval_WhileCashInCanBePostedFromDraft()
    {
        var cashAccount = Guid.NewGuid();
        var expense = Guid.NewGuid();

        CashTransaction.Create(BranchId, Guid.NewGuid(), cashAccount, CashDirection.Out, Date, "listrik", null,
                [new CashTransactionLineInput(cashAccount, null, null, new Money(1m))])
            .Error.ShouldBe(CashTransactionErrors.LineOnCashAccount);

        CashTransaction cashOut = CashTransaction.Create(BranchId, Guid.NewGuid(), cashAccount, CashDirection.Out, Date, "listrik", null,
            [new CashTransactionLineInput(expense, null, null, new Money(750_000m))]).Value;

        cashOut.EnsurePostable().IsFailure.ShouldBeTrue();
        cashOut.Approve(Guid.NewGuid(), DateTime.UtcNow);
        cashOut.Post("BKK/1", null, DateTime.UtcNow).IsSuccess.ShouldBeTrue();

        CashTransaction cashIn = CashTransaction.Create(BranchId, Guid.NewGuid(), cashAccount, CashDirection.In, Date, "bunga", null,
            [new CashTransactionLineInput(Guid.NewGuid(), null, null, new Money(50_000m))]).Value;

        cashIn.Post("BKM/1", null, DateTime.UtcNow).IsSuccess.ShouldBeTrue();
        cashIn.DomainEvents.OfType<CashTransactionPostedDomainEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void BankTransfer_Should_StayWithinOneBranch()
    {
        CashBankAccount bank = Bank(BranchId);
        CashBankAccount pettyCash = CashBankAccount.Create("KK", "Kas Kecil", CashBankAccountType.PettyCash, BranchId, Guid.NewGuid(), true, null, null).Value;

        BankTransfer.Create("TRF/1", bank, bank, Date, new Money(1m), null, null).Error.ShouldBe(BankTransferErrors.SameAccount);
        BankTransfer.Create("TRF/1", bank, Bank(Guid.NewGuid()), Date, new Money(1m), null, null).Error.ShouldBe(BankTransferErrors.DifferentBranches);
        BankTransfer.Create("TRF/1", bank, pettyCash, Date, new Money(2_000_000m), null, null).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CashBankAccount_Should_NeedAPostableAssetAccount_AndBankDetails()
    {
        CashBankAccount.Create("BCA", "BCA", CashBankAccountType.Bank, BranchId, Guid.NewGuid(), false, "BCA", "123")
            .Error.ShouldBe(CashBankErrors.InvalidAccount);

        CashBankAccount.Create("BCA", "BCA", CashBankAccountType.Bank, BranchId, Guid.NewGuid(), true, null, null)
            .Error.ShouldBe(CashBankErrors.BankDetailsRequired);
    }

    [Fact]
    public void Reconciliation_Should_Complete_OnlyWhenMatchedAndBalanced()
    {
        BankReconciliation.Start(CashBankAccount.Create("KAS", "Kas", CashBankAccountType.Cash, BranchId, Guid.NewGuid(), true, null, null).Value,
                Date, Money.Zero, null)
            .Error.ShouldBe(BankReconciliationErrors.NotABankAccount);

        BankReconciliation reconciliation = BankReconciliation.Start(Bank(BranchId), Date.AddDays(29), new Money(9_950_000m), null).Value;
        reconciliation.AddLines(
        [
            (Date.AddDays(2), "SETORAN", new Money(10_000_000m)),
            (Date.AddDays(29), "BIAYA ADM", new Money(-50_000m))
        ]).IsSuccess.ShouldBeTrue();

        var deposit = new BookEntry(Guid.NewGuid(), 1, Date.AddDays(1), new Money(10_000_000m));
        reconciliation.Match(1, deposit with { Amount = new Money(9_000_000m) }).Error.Code.ShouldBe("BankReconciliations.AmountMismatch");
        reconciliation.Match(1, deposit).IsSuccess.ShouldBeTrue();

        // Book: deposit 10 jt, cheque 2 jt not yet cleared; the bank fee is not booked yet.
        reconciliation.Complete(new Money(8_000_000m), new Money(-2_000_000m), null, DateTime.UtcNow)
            .Error.ShouldBe(BankReconciliationErrors.UnmatchedLines(1));

        // After booking the bank fee (cash-out) and matching it: book 7,95 jt, uncleared −2 jt → 9,95 jt = statement.
        reconciliation.Match(2, new BookEntry(Guid.NewGuid(), 2, Date.AddDays(29), new Money(-50_000m))).IsSuccess.ShouldBeTrue();
        reconciliation.Complete(new Money(7_950_000m), new Money(-1_000_000m), null, DateTime.UtcNow)
            .Error.Code.ShouldBe("BankReconciliations.NotBalanced");
        reconciliation.Complete(new Money(7_950_000m), new Money(-2_000_000m), null, DateTime.UtcNow).IsSuccess.ShouldBeTrue();

        reconciliation.AddLines([(Date, "x", new Money(1m))]).Error.ShouldBe(BankReconciliationErrors.AlreadyCompleted);
        BankReconciliation.Start(Bank(BranchId), Date.AddDays(29), Money.Zero, Date.AddDays(29)).Error.Code
            .ShouldBe("BankReconciliations.NotAfterLastReconciliation");
    }

    private static CashBankAccount Bank(Guid branchId) =>
        CashBankAccount.Create("BCA", "BCA Operasional", CashBankAccountType.Bank, branchId, Guid.NewGuid(), true, "BCA", "123").Value;
}
