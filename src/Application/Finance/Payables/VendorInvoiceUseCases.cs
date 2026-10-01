using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Application.Finance.AutoJournal;
using Application.Finance.Journals;
using Application.Inventory;
using Application.Production;
using Domain.Finance.JournalMappings;
using Domain.Finance.Payables;
using Domain.Inventory.GoodsReceipts;
using Domain.MasterData.TaxCodes;
using Domain.MasterData.Vendors;
using Domain.Procurement.PurchaseOrders;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Payables;

/// <param name="Quantity">Billed quantity in the purchase order line's unit, at most the not yet billed received quantity.</param>
/// <param name="UnitPrice">Price per order unit on the vendor's invoice, excluding VAT.</param>
public sealed record VendorInvoiceLineRequest(Guid GoodsReceiptId, int GoodsReceiptLineNumber, decimal Quantity, decimal UnitPrice);

/// <summary>
/// Registers a vendor invoice as a draft against received goods (3-way match). Returns the invoice id; the internal
/// number is given when posting.
/// </summary>
/// <param name="IncomeTaxCodeId">PPh withheld from the vendor (e.g. PPh 22/23), if any.</param>
public sealed record CreateVendorInvoiceCommand(
    Guid BranchId,
    Guid VendorId,
    string VendorInvoiceNumber,
    string? TaxInvoiceNumber,
    DateOnly InvoiceDate,
    Guid? IncomeTaxCodeId,
    string? Notes,
    IReadOnlyList<VendorInvoiceLineRequest> Lines) : ICommand<Guid>;

/// <summary>
/// Posts a draft vendor invoice. A price difference above the vendor's tolerance needs
/// <paramref name="VarianceApprovalReason"/> (endpoint guarded by <c>payables:approve-variance</c>). Returns the number.
/// </summary>
public sealed record PostVendorInvoiceCommand(Guid VendorInvoiceId, string? VarianceApprovalReason) : ICommand<string>;

public sealed record CancelVendorInvoiceCommand(Guid VendorInvoiceId, string Reason) : ICommand;

internal sealed class CreateVendorInvoiceCommandValidator : AbstractValidator<CreateVendorInvoiceCommand>
{
    public CreateVendorInvoiceCommandValidator()
    {
        RuleFor(c => c.BranchId).NotEmpty();
        RuleFor(c => c.VendorId).NotEmpty();
        RuleFor(c => c.VendorInvoiceNumber).NotEmpty().MaximumLength(50);
        RuleFor(c => c.TaxInvoiceNumber).MaximumLength(50);
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.GoodsReceiptId).NotEmpty();
            line.RuleFor(l => l.GoodsReceiptLineNumber).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThan(0);
        });
    }
}

internal sealed class PostVendorInvoiceCommandValidator : AbstractValidator<PostVendorInvoiceCommand>
{
    public PostVendorInvoiceCommandValidator()
    {
        RuleFor(c => c.VendorInvoiceId).NotEmpty();
        RuleFor(c => c.VarianceApprovalReason).NotEmpty().MaximumLength(500).When(c => c.VarianceApprovalReason is not null);
    }
}

internal sealed class CancelVendorInvoiceCommandValidator : AbstractValidator<CancelVendorInvoiceCommand>
{
    public CancelVendorInvoiceCommandValidator()
    {
        RuleFor(c => c.VendorInvoiceId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    }
}

internal static class VendorInvoiceSupport
{
    public static async Task<Result<VendorInvoice>> LoadAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid vendorInvoiceId,
        CancellationToken cancellationToken)
    {
        VendorInvoice? invoice = await context.VendorInvoices
            .Include(i => i.Lines)
            .SingleOrDefaultAsync(i => i.Id == vendorInvoiceId, cancellationToken);

        if (invoice is null)
        {
            return Result.Failure<VendorInvoice>(VendorInvoiceErrors.NotFound(vendorInvoiceId));
        }

        Result access = await branchAccess.EnsureAccessAsync(invoice.BranchId, cancellationToken);

        return access.IsSuccess ? invoice : Result.Failure<VendorInvoice>(access.Error);
    }
}

internal sealed class CreateVendorInvoiceCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<CreateVendorInvoiceCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateVendorInvoiceCommand command, CancellationToken cancellationToken)
    {
        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.InvoiceDate, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return Result.Failure<Guid>(notFuture.Error);
        }

        Result access = await branchAccess.EnsureAccessAsync(command.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        Vendor? vendor = await context.Vendors.AsNoTracking().SingleOrDefaultAsync(v => v.Id == command.VendorId, cancellationToken);
        if (vendor is null)
        {
            return Result.Failure<Guid>(VendorErrors.NotFound(command.VendorId));
        }

        string vendorInvoiceNumber = command.VendorInvoiceNumber.Trim();
        bool duplicate = await context.VendorInvoices.AnyAsync(
            i => i.VendorId == command.VendorId && i.VendorInvoiceNumber == vendorInvoiceNumber && i.Status != VendorInvoiceStatus.Cancelled,
            cancellationToken);

        if (duplicate)
        {
            return Result.Failure<Guid>(VendorInvoiceErrors.DuplicateVendorInvoiceNumber(vendorInvoiceNumber));
        }

        Result<List<VendorInvoiceLineInput>> lines = await ToDomainAsync(command.Lines, cancellationToken);
        if (lines.IsFailure)
        {
            return Result.Failure<Guid>(lines.Error);
        }

        var taxCodeIds = lines.Value
            .Where(l => l.OrderLine.TaxCodeId is not null)
            .Select(l => l.OrderLine.TaxCodeId!.Value)
            .Append(command.IncomeTaxCodeId ?? Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        Dictionary<Guid, TaxCode> taxCodes = await context.TaxCodes.AsNoTracking()
            .Include(t => t.Rates)
            .Where(t => taxCodeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        if (command.IncomeTaxCodeId is not null &&
            (!taxCodes.TryGetValue(command.IncomeTaxCodeId.Value, out TaxCode? incomeTax) || incomeTax.Type != TaxType.IncomeTax || !incomeTax.IsActive))
        {
            return Result.Failure<Guid>(VendorInvoiceErrors.InvalidIncomeTaxCode);
        }

        Result<VendorInvoice> invoice = VendorInvoice.CreateDraft(
            command.BranchId,
            vendor.Id,
            vendor.PaymentTermDays,
            vendorInvoiceNumber,
            command.TaxInvoiceNumber,
            command.InvoiceDate,
            command.Notes,
            command.IncomeTaxCodeId,
            lines.Value,
            taxCodes);

        if (invoice.IsFailure)
        {
            return Result.Failure<Guid>(invoice.Error);
        }

        context.VendorInvoices.Add(invoice.Value);

        await context.SaveChangesAsync(cancellationToken);

        return invoice.Value.Id;
    }

    /// <summary>
    /// Loads the goods receipts (tracked, the billed quantity is registered on them) and the purchase order lines they
    /// were received against.
    /// </summary>
    private async Task<Result<List<VendorInvoiceLineInput>>> ToDomainAsync(
        IReadOnlyList<VendorInvoiceLineRequest> requests,
        CancellationToken cancellationToken)
    {
        var receiptIds = requests.Select(r => r.GoodsReceiptId).Distinct().ToList();

        List<GoodsReceipt> receipts = await context.GoodsReceipts
            .Include(r => r.Lines)
            .Where(r => receiptIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        Guid missing = receiptIds.Find(id => receipts.TrueForAll(r => r.Id != id));
        if (missing != Guid.Empty)
        {
            return Result.Failure<List<VendorInvoiceLineInput>>(GoodsReceiptErrors.NotFound(missing));
        }

        var orderIds = receipts.Select(r => r.PurchaseOrderId).Distinct().ToList();
        List<PurchaseOrder> orders = await context.PurchaseOrders.AsNoTracking()
            .Include(o => o.Lines)
            .Where(o => orderIds.Contains(o.Id))
            .ToListAsync(cancellationToken);

        var lines = new List<VendorInvoiceLineInput>();

        foreach (VendorInvoiceLineRequest request in requests)
        {
            GoodsReceipt receipt = receipts.Single(r => r.Id == request.GoodsReceiptId);
            GoodsReceiptLine? receiptLine = receipt.Lines.FirstOrDefault(l => l.LineNumber == request.GoodsReceiptLineNumber);

            if (receiptLine is null)
            {
                return Result.Failure<List<VendorInvoiceLineInput>>(GoodsReceiptErrors.LineNotFound(request.GoodsReceiptLineNumber));
            }

            PurchaseOrderLine orderLine = orders
                .Single(o => o.Id == receipt.PurchaseOrderId)
                .Lines.Single(l => l.LineNumber == receiptLine.PurchaseOrderLineNumber);

            lines.Add(new VendorInvoiceLineInput(
                receipt, receiptLine.LineNumber, orderLine, request.Quantity, new Money(request.UnitPrice)));
        }

        return lines;
    }
}

internal sealed class PostVendorInvoiceCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<PostVendorInvoiceCommand, string>
{
    private const string DocumentPrefix = "VI";

    public async Task<Result<string>> Handle(PostVendorInvoiceCommand command, CancellationToken cancellationToken)
    {
        Result<VendorInvoice> invoice = await VendorInvoiceSupport.LoadAsync(context, branchAccess, command.VendorInvoiceId, cancellationToken);
        if (invoice.IsFailure)
        {
            return Result.Failure<string>(invoice.Error);
        }

        decimal tolerance = await context.Vendors
            .Where(v => v.Id == invoice.Value.VendorId)
            .Select(v => v.PriceTolerancePercent)
            .SingleAsync(cancellationToken);

        // Checked before taking a number so a rejected post does not consume one.
        Result postable = invoice.Value.EnsurePostable(tolerance, command.VarianceApprovalReason);
        if (postable.IsFailure)
        {
            return Result.Failure<string>(postable.Error);
        }

        // The journal is posted from the outbox; reject a closed period now rather than dead-letter it later.
        Result<Domain.Finance.FiscalPeriods.FiscalPeriod> period = await JournalSupport.FindPeriodAsync(
            context, invoice.Value.InvoiceDate, cancellationToken);

        if (period.IsFailure)
        {
            return Result.Failure<string>(period.Error);
        }

        string number = await numberGenerator.NextForBranchAsync(
            context, DocumentPrefix, invoice.Value.BranchId, invoice.Value.InvoiceDate, cancellationToken);

        Result posted = invoice.Value.Post(number, tolerance, command.VarianceApprovalReason, userContext.UserId, dateTimeProvider.UtcNow);
        if (posted.IsFailure)
        {
            return Result.Failure<string>(posted.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return number;
    }
}

internal sealed class CancelVendorInvoiceCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CancelVendorInvoiceCommand>
{
    public async Task<Result> Handle(CancelVendorInvoiceCommand command, CancellationToken cancellationToken)
    {
        Result<VendorInvoice> invoice = await VendorInvoiceSupport.LoadAsync(context, branchAccess, command.VendorInvoiceId, cancellationToken);
        if (invoice.IsFailure)
        {
            return invoice;
        }

        var receiptIds = invoice.Value.Lines.Select(l => l.GoodsReceiptId).Distinct().ToList();
        List<GoodsReceipt> receipts = await context.GoodsReceipts
            .Include(r => r.Lines)
            .Where(r => receiptIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        Result result = invoice.Value.Cancel(command.Reason, receipts);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>
/// Journals a posted vendor invoice: Dr hutang belum ditagih (receipt value) / Cr hutang usaha, the price variance
/// (Dr selisih / Cr hutang usaha, sides swapped when negative), Dr PPN masukan / Cr hutang usaha and
/// Dr hutang usaha / Cr hutang PPh for the income tax withheld.
/// </summary>
internal sealed class VendorInvoicePostedDomainEventHandler(IApplicationDbContext context, IAutoJournalService autoJournal)
    : IDomainEventHandler<VendorInvoicePostedDomainEvent>
{
    public async Task Handle(VendorInvoicePostedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        VendorInvoice invoice = await context.VendorInvoices.AsNoTracking()
            .SingleAsync(i => i.Id == domainEvent.VendorInvoiceId, cancellationToken);

        string vendor = await context.Vendors
            .Where(v => v.Id == invoice.VendorId)
            .Select(v => v.Name)
            .SingleAsync(cancellationToken);

        await InventoryAccounting.PostAsync(autoJournal, new AccountingEntry(
            AccountingEvents.VendorInvoice,
            invoice.Id,
            invoice.BranchId,
            invoice.InvoiceDate,
            $"Tagihan {invoice.Number} ({invoice.VendorInvoiceNumber}) dari {vendor}",
            [
                new AccountingAmount("GoodsValue", invoice.GoodsValue),
                new AccountingAmount("PriceVariance", invoice.PriceVariance),
                new AccountingAmount("InputVat", invoice.VatAmount),
                new AccountingAmount("IncomeTaxWithheld", invoice.IncomeTaxAmount)
            ]),
            cancellationToken);
    }
}
