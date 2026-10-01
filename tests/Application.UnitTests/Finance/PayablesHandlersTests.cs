using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Numbering;
using Application.Finance.Payables;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.Finance.Accounts;
using Domain.Finance.CashBank;
using Domain.Finance.FiscalPeriods;
using Domain.Finance.Payables;
using Domain.Inventory.GoodsReceipts;
using Domain.MasterData.Branches;
using Domain.MasterData.Items;
using Domain.MasterData.Uoms;
using Domain.MasterData.Vendors;
using Domain.MasterData.Warehouses;
using Domain.Procurement.PurchaseOrders;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Finance;

public sealed class PayablesHandlersTests : BaseHandlerTest
{
    private static readonly DateOnly Date = new(2026, 9, 1);

    [Fact]
    public async Task VendorInvoice_Then_PaymentVoucher_Should_SettleThePayable()
    {
        await using TestDbContext context = CreateDbContext();
        Setup s = await SeedAsync(context);

        var create = new CreateVendorInvoiceCommandHandler(context, AllBranches(), Clock(), CreateAttachments(context));
        var line = new VendorInvoiceLineRequest(s.Receipt.Id, 1, 30m, 435_000m);

        Guid invoiceId = (await create.Handle(
            new CreateVendorInvoiceCommand(s.BranchId, s.Vendor.Id, "INV-PKN-01", "010.000-26.00000001", Date, null, null, [line]),
            CancellationToken.None)).Value;

        // Same vendor invoice number again, and the receipt is fully billed already.
        (await create.Handle(
                new CreateVendorInvoiceCommand(s.BranchId, s.Vendor.Id, "INV-PKN-01", null, Date, null, null, [line]),
                CancellationToken.None))
            .Error.ShouldBe(VendorInvoiceErrors.DuplicateVendorInvoiceNumber("INV-PKN-01"));

        (await create.Handle(
                new CreateVendorInvoiceCommand(s.BranchId, s.Vendor.Id, "INV-PKN-02", null, Date, null, null, [line with { Quantity = 1m }]),
                CancellationToken.None))
            .Error.Code.ShouldBe("GoodsReceipts.OverInvoiced");

        // Rp 5.000 above the order price = 1,1628%, above the vendor's 1% tolerance.
        var post = new PostVendorInvoiceCommandHandler(context, AllBranches(), Numbers(), User(), Clock());
        (await post.Handle(new PostVendorInvoiceCommand(invoiceId, null), CancellationToken.None))
            .Error.Code.ShouldBe("VendorInvoices.PriceVarianceAboveTolerance");

        (await post.Handle(new PostVendorInvoiceCommand(invoiceId, "kenaikan harga pabrik"), CancellationToken.None)).Value.ShouldStartWith("VI/");

        VendorInvoice invoice = await context.VendorInvoices.SingleAsync();
        invoice.Total.ShouldBe(new Money(13_050_000m));
        invoice.PriceVariance.ShouldBe(new Money(150_000m));

        Result<CreatePaymentVoucherResponse> voucher = await new CreatePaymentVoucherCommandHandler(context, AllBranches(), Numbers(), CreateAttachments(context))
            .Handle(
                new CreatePaymentVoucherCommand(s.Bank.Id, s.Vendor.Id, Date.AddDays(10), "TRF", null,
                    [new PaymentAllocationRequest(invoiceId, 13_050_000m)]),
                CancellationToken.None);

        (await new ApprovePaymentVoucherCommandHandler(context, AllBranches(), User(), Clock())
            .Handle(new ApprovePaymentVoucherCommand(voucher.Value.Id), CancellationToken.None)).IsSuccess.ShouldBeTrue();

        (await new PayPaymentVoucherCommandHandler(context, AllBranches(), User(), Clock())
            .Handle(new PayPaymentVoucherCommand(voucher.Value.Id, Date.AddDays(12)), CancellationToken.None)).IsSuccess.ShouldBeTrue();

        invoice.Status.ShouldBe(VendorInvoiceStatus.Paid);
        PaymentVoucher paid = await context.PaymentVouchers.SingleAsync();
        paid.Status.ShouldBe(PaymentVoucherStatus.Paid);
        paid.PaymentDate.ShouldBe(Date.AddDays(12));
    }

    private static IBranchAccess AllBranches()
    {
        IBranchAccess branchAccess = Substitute.For<IBranchAccess>();
        branchAccess.GetScopeAsync(Arg.Any<CancellationToken>()).Returns(BranchScope.All);

        return branchAccess;
    }

    private static IDateTimeProvider Clock()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));

        return clock;
    }

    private static IUserContext User()
    {
        IUserContext user = Substitute.For<IUserContext>();
        user.UserId.Returns(Guid.NewGuid());

        return user;
    }

    private static IDocumentNumberGenerator Numbers()
    {
        IDocumentNumberGenerator numbers = Substitute.For<IDocumentNumberGenerator>();
        numbers.NextAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(call => $"{call.ArgAt<string>(0)}/BDG/{Guid.NewGuid():N}");

        return numbers;
    }

    /// <summary>
    /// A vendor with a 1% price tolerance, an approved PO of 30 SAK feed @ Rp 430.000 fully received, and a bank account.
    /// </summary>
    private static async Task<Setup> SeedAsync(TestDbContext context)
    {
        var branch = Branch.Create("BDG", "Bandung", null, null);
        var kg = Uom.Create("KG", "Kilogram");
        var sak = Uom.Create("SAK", "Sak");
        var feed = Item.Create("PKN", "Pakan", ItemCategory.Feed, kg.Id, null);
        feed.SetConversions([(sak.Id, 50m)]);
        var vendor = Vendor.Create("V-PKN", "PT Pakan", TaxIdentity.None, null, null, null, 30, BankAccount.None);
        vendor.SetPriceTolerance(1m);

        PurchaseOrder order = PurchaseOrder.Create("PO/1", branch.Id, vendor.Id, Date, null, null,
            [new PurchaseOrderLineInput(feed.Id, sak.Id, 30m, new Money(430_000m), null)]).Value;
        order.Approve(Guid.NewGuid(), DateTime.UtcNow);

        GoodsReceipt receipt = GoodsReceipt.Create("BPB/1", order, Warehouse.CreateCentral("GI", "Gudang", branch.Id, null), null, Date,
            null, null, [new GoodsReceiptLineInput(1, 30m, 1_500m)], new Dictionary<Guid, ItemCategory> { [feed.Id] = ItemCategory.Feed }).Value;

        Account bankAccount = Account.Create("1-1201", "Bank", AccountType.Asset, null, true, null).Value;
        CashBankAccount bank = CashBankAccount.Create("BCA", "BCA", CashBankAccountType.Bank, branch.Id, bankAccount.Id, true, "BCA", "1").Value;

        context.Branches.Add(branch);
        context.Uoms.AddRange(kg, sak);
        context.Items.Add(feed);
        context.Vendors.Add(vendor);
        context.PurchaseOrders.Add(order);
        context.GoodsReceipts.Add(receipt);
        context.Accounts.Add(bankAccount);
        context.CashBankAccounts.Add(bank);
        context.FiscalPeriods.AddRange(FiscalPeriod.CreateYear(2026));
        await context.SaveChangesAsync();

        return new Setup(branch.Id, vendor, receipt, bank);
    }

    private sealed record Setup(Guid BranchId, Vendor Vendor, GoodsReceipt Receipt, CashBankAccount Bank);
}
