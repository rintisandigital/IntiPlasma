using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Application.Finance.AutoJournal;
using Application.Finance.Journals;
using Application.Inventory;
using Application.Production;
using Domain.Costing.PlasmaSettlements;
using Domain.Finance.CashBank;
using Domain.Finance.JournalMappings;
using Domain.Finance.Payables;
using Domain.MasterData.Farmers;
using Domain.MasterData.Vendors;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Payables;

public sealed record PaymentAllocationRequest(Guid VendorInvoiceId, decimal Amount);

public sealed record SettlementPaymentRequest(Guid PlasmaSettlementId, decimal Amount);

/// <summary>
/// Drafts a payment voucher paying posted invoices of a vendor from a cash/bank account (maker).
/// </summary>
public sealed record CreatePaymentVoucherCommand(
    Guid CashBankAccountId,
    Guid VendorId,
    DateOnly PaymentDate,
    string? Reference,
    string? Notes,
    IReadOnlyList<PaymentAllocationRequest> Allocations) : ICommand<CreatePaymentVoucherResponse>;

/// <summary>
/// Drafts a payment voucher paying approved settlements of a plasma farmer from a cash/bank account (maker).
/// </summary>
public sealed record CreatePlasmaPaymentVoucherCommand(
    Guid CashBankAccountId,
    Guid FarmerId,
    DateOnly PaymentDate,
    string? Reference,
    string? Notes,
    IReadOnlyList<SettlementPaymentRequest> Allocations) : ICommand<CreatePaymentVoucherResponse>;

public sealed record CreatePaymentVoucherResponse(Guid Id, string Number, decimal Amount);

/// <summary>
/// Approval by someone other than the creator (checker).
/// </summary>
public sealed record ApprovePaymentVoucherCommand(Guid PaymentVoucherId) : ICommand;

/// <summary>
/// Pays an approved voucher on the actual payment date: registers the payments on the documents and journals them.
/// </summary>
public sealed record PayPaymentVoucherCommand(Guid PaymentVoucherId, DateOnly PaymentDate) : ICommand;

public sealed record CancelPaymentVoucherCommand(Guid PaymentVoucherId, string Reason) : ICommand;

internal sealed class CreatePaymentVoucherCommandValidator : AbstractValidator<CreatePaymentVoucherCommand>
{
    public CreatePaymentVoucherCommandValidator()
    {
        RuleFor(c => c.CashBankAccountId).NotEmpty();
        RuleFor(c => c.VendorId).NotEmpty();
        RuleFor(c => c.Reference).MaximumLength(100);
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Allocations).NotEmpty();
        RuleForEach(c => c.Allocations).ChildRules(allocation =>
        {
            allocation.RuleFor(a => a.VendorInvoiceId).NotEmpty();
            allocation.RuleFor(a => a.Amount).GreaterThan(0);
        });
    }
}

internal sealed class CreatePlasmaPaymentVoucherCommandValidator : AbstractValidator<CreatePlasmaPaymentVoucherCommand>
{
    public CreatePlasmaPaymentVoucherCommandValidator()
    {
        RuleFor(c => c.CashBankAccountId).NotEmpty();
        RuleFor(c => c.FarmerId).NotEmpty();
        RuleFor(c => c.Reference).MaximumLength(100);
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Allocations).NotEmpty();
        RuleForEach(c => c.Allocations).ChildRules(allocation =>
        {
            allocation.RuleFor(a => a.PlasmaSettlementId).NotEmpty();
            allocation.RuleFor(a => a.Amount).GreaterThan(0);
        });
    }
}

internal sealed class CancelPaymentVoucherCommandValidator : AbstractValidator<CancelPaymentVoucherCommand>
{
    public CancelPaymentVoucherCommandValidator()
    {
        RuleFor(c => c.PaymentVoucherId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    }
}

internal static class PaymentVoucherSupport
{
    private const string DocumentPrefix = "PV";

    public static async Task<Result<PaymentVoucher>> LoadAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid paymentVoucherId,
        CancellationToken cancellationToken)
    {
        PaymentVoucher? voucher = await context.PaymentVouchers
            .Include(v => v.Allocations)
            .Include(v => v.SettlementAllocations)
            .SingleOrDefaultAsync(v => v.Id == paymentVoucherId, cancellationToken);

        if (voucher is null)
        {
            return Result.Failure<PaymentVoucher>(PaymentVoucherErrors.NotFound(paymentVoucherId));
        }

        Result access = await branchAccess.EnsureAccessAsync(voucher.BranchId, cancellationToken);

        return access.IsSuccess ? voucher : Result.Failure<PaymentVoucher>(access.Error);
    }

    /// <summary>
    /// The documents a voucher pays: vendor invoices or plasma settlements.
    /// </summary>
    public static async Task<List<IPayable>> LoadDocumentsAsync(
        IApplicationDbContext context,
        PayeeType payeeType,
        IReadOnlyCollection<Guid> documentIds,
        bool track,
        CancellationToken cancellationToken)
    {
        if (payeeType == PayeeType.Vendor)
        {
            IQueryable<VendorInvoice> invoices = track ? context.VendorInvoices : context.VendorInvoices.AsNoTracking();
            return [.. await invoices.Where(i => documentIds.Contains(i.Id)).ToListAsync(cancellationToken)];
        }

        IQueryable<PlasmaSettlement> settlements = track ? context.PlasmaSettlements : context.PlasmaSettlements.AsNoTracking();
        return [.. await settlements.Where(s => documentIds.Contains(s.Id)).ToListAsync(cancellationToken)];
    }

    public static async Task<Result<CreatePaymentVoucherResponse>> CreateAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        IDocumentNumberGenerator numberGenerator,
        PayeeType payeeType,
        Guid payeeId,
        Guid cashBankAccountId,
        DateOnly paymentDate,
        string? reference,
        string? notes,
        IReadOnlyList<(Guid DocumentId, decimal Amount)> requests,
        CancellationToken cancellationToken)
    {
        CashBankAccount? cashBank = await context.CashBankAccounts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == cashBankAccountId, cancellationToken);

        if (cashBank is null)
        {
            return Result.Failure<CreatePaymentVoucherResponse>(CashBankErrors.NotFound(cashBankAccountId));
        }

        Result access = await branchAccess.EnsureAccessAsync(cashBank.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CreatePaymentVoucherResponse>(access.Error);
        }

        if (!cashBank.IsActive)
        {
            return Result.Failure<CreatePaymentVoucherResponse>(CashBankErrors.Unusable(cashBank.Id));
        }

        var documentIds = requests.Select(r => r.DocumentId).Distinct().ToList();
        List<IPayable> documents = await LoadDocumentsAsync(context, payeeType, documentIds, track: false, cancellationToken);

        Guid missing = documentIds.Find(id => documents.TrueForAll(d => d.Id != id));
        if (missing != Guid.Empty)
        {
            return Result.Failure<CreatePaymentVoucherResponse>(payeeType == PayeeType.Vendor
                ? VendorInvoiceErrors.NotFound(missing)
                : PlasmaSettlementErrors.NotFound(missing));
        }

        List<(IPayable Document, Money Amount)> allocations =
            [.. requests.Select(r => (documents.Single(d => d.Id == r.DocumentId), new Money(r.Amount)))];

        // Validated with a placeholder number first so a rejected voucher does not consume a document number.
        Result<PaymentVoucher> validation = PaymentVoucher.Create(
            string.Empty, cashBank.BranchId, payeeType, payeeId, cashBank.Id, paymentDate, reference, notes, allocations);

        if (validation.IsFailure)
        {
            return Result.Failure<CreatePaymentVoucherResponse>(validation.Error);
        }

        string number = await numberGenerator.NextForBranchAsync(context, DocumentPrefix, cashBank.BranchId, paymentDate, cancellationToken);

        PaymentVoucher voucher = PaymentVoucher.Create(
            number, cashBank.BranchId, payeeType, payeeId, cashBank.Id, paymentDate, reference, notes, allocations).Value;

        context.PaymentVouchers.Add(voucher);

        await context.SaveChangesAsync(cancellationToken);

        return new CreatePaymentVoucherResponse(voucher.Id, voucher.Number, voucher.Amount.Amount);
    }
}

internal sealed class CreatePaymentVoucherCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator) : ICommandHandler<CreatePaymentVoucherCommand, CreatePaymentVoucherResponse>
{
    public async Task<Result<CreatePaymentVoucherResponse>> Handle(CreatePaymentVoucherCommand command, CancellationToken cancellationToken)
    {
        if (!await context.Vendors.AnyAsync(v => v.Id == command.VendorId, cancellationToken))
        {
            return Result.Failure<CreatePaymentVoucherResponse>(VendorErrors.NotFound(command.VendorId));
        }

        return await PaymentVoucherSupport.CreateAsync(
            context, branchAccess, numberGenerator, PayeeType.Vendor, command.VendorId, command.CashBankAccountId, command.PaymentDate,
            command.Reference, command.Notes, [.. command.Allocations.Select(a => (a.VendorInvoiceId, a.Amount))], cancellationToken);
    }
}

internal sealed class CreatePlasmaPaymentVoucherCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator) : ICommandHandler<CreatePlasmaPaymentVoucherCommand, CreatePaymentVoucherResponse>
{
    public async Task<Result<CreatePaymentVoucherResponse>> Handle(CreatePlasmaPaymentVoucherCommand command, CancellationToken cancellationToken)
    {
        if (!await context.Farmers.AnyAsync(f => f.Id == command.FarmerId, cancellationToken))
        {
            return Result.Failure<CreatePaymentVoucherResponse>(FarmerErrors.NotFound(command.FarmerId));
        }

        return await PaymentVoucherSupport.CreateAsync(
            context, branchAccess, numberGenerator, PayeeType.Farmer, command.FarmerId, command.CashBankAccountId, command.PaymentDate,
            command.Reference, command.Notes, [.. command.Allocations.Select(a => (a.PlasmaSettlementId, a.Amount))], cancellationToken);
    }
}

internal sealed class ApprovePaymentVoucherCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ApprovePaymentVoucherCommand>
{
    public async Task<Result> Handle(ApprovePaymentVoucherCommand command, CancellationToken cancellationToken)
    {
        Result<PaymentVoucher> voucher = await PaymentVoucherSupport.LoadAsync(context, branchAccess, command.PaymentVoucherId, cancellationToken);
        if (voucher.IsFailure)
        {
            return voucher;
        }

        Result result = voucher.Value.Approve(userContext.UserId, dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class PayPaymentVoucherCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<PayPaymentVoucherCommand>
{
    public async Task<Result> Handle(PayPaymentVoucherCommand command, CancellationToken cancellationToken)
    {
        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.PaymentDate, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return notFuture;
        }

        Result<PaymentVoucher> voucher = await PaymentVoucherSupport.LoadAsync(context, branchAccess, command.PaymentVoucherId, cancellationToken);
        if (voucher.IsFailure)
        {
            return voucher;
        }

        // The journal is posted from the outbox; reject a closed period now rather than dead-letter it later.
        Result<Domain.Finance.FiscalPeriods.FiscalPeriod> period = await JournalSupport.FindPeriodAsync(context, command.PaymentDate, cancellationToken);
        if (period.IsFailure)
        {
            return period;
        }

        List<IPayable> documents = await PaymentVoucherSupport.LoadDocumentsAsync(
            context, voucher.Value.PayeeType, [.. voucher.Value.DocumentAmounts.Select(a => a.DocumentId)], track: true, cancellationToken);

        Result paid = voucher.Value.Pay(command.PaymentDate, documents, userContext.UserId, dateTimeProvider.UtcNow);
        if (paid.IsFailure)
        {
            return paid;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class CancelPaymentVoucherCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CancelPaymentVoucherCommand>
{
    public async Task<Result> Handle(CancelPaymentVoucherCommand command, CancellationToken cancellationToken)
    {
        Result<PaymentVoucher> voucher = await PaymentVoucherSupport.LoadAsync(context, branchAccess, command.PaymentVoucherId, cancellationToken);
        if (voucher.IsFailure)
        {
            return voucher;
        }

        Result result = voucher.Value.Cancel(command.Reason);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>
/// Journals a paid voucher: Dr hutang usaha (vendor) or hutang plasma (farmer) / Cr the cash/bank account of the voucher.
/// </summary>
internal sealed class PaymentVoucherPaidDomainEventHandler(IApplicationDbContext context, IAutoJournalService autoJournal)
    : IDomainEventHandler<PaymentVoucherPaidDomainEvent>
{
    public async Task Handle(PaymentVoucherPaidDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        PaymentVoucher voucher = await context.PaymentVouchers.AsNoTracking()
            .SingleAsync(v => v.Id == domainEvent.PaymentVoucherId, cancellationToken);

        Guid cashAccountId = await context.CashBankAccounts
            .Where(a => a.Id == voucher.CashBankAccountId)
            .Select(a => a.AccountId)
            .SingleAsync(cancellationToken);

        bool vendor = voucher.PayeeType == PayeeType.Vendor;
        string payee = vendor
            ? await context.Vendors.Where(v => v.Id == voucher.VendorId).Select(v => v.Name).SingleAsync(cancellationToken)
            : await context.Farmers.Where(f => f.Id == voucher.FarmerId).Select(f => f.Name).SingleAsync(cancellationToken);

        await InventoryAccounting.PostAsync(autoJournal, new AccountingEntry(
            vendor ? AccountingEvents.VendorPayment : AccountingEvents.PlasmaPayment,
            voucher.Id,
            voucher.BranchId,
            voucher.PaymentDate,
            $"Pembayaran {voucher.Number} kepada {payee}",
            [new AccountingAmount("Paid", voucher.Amount, CreditAccountId: cashAccountId)]),
            cancellationToken);
    }
}
