using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Application.Finance.AutoJournal;
using Application.Finance.Journals;
using Application.Inventory;
using Application.Production;
using Domain.Finance.CashBank;
using Domain.Finance.JournalMappings;
using Domain.Finance.Payables;
using Domain.MasterData.Vendors;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Payables;

public sealed record PaymentAllocationRequest(Guid VendorInvoiceId, decimal Amount);

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

public sealed record CreatePaymentVoucherResponse(Guid Id, string Number, decimal Amount);

/// <summary>
/// Approval by someone other than the creator (checker).
/// </summary>
public sealed record ApprovePaymentVoucherCommand(Guid PaymentVoucherId) : ICommand;

/// <summary>
/// Pays an approved voucher on the actual payment date: registers the payments on the invoices and journals them.
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
    public static async Task<Result<PaymentVoucher>> LoadAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid paymentVoucherId,
        CancellationToken cancellationToken)
    {
        PaymentVoucher? voucher = await context.PaymentVouchers
            .Include(v => v.Allocations)
            .SingleOrDefaultAsync(v => v.Id == paymentVoucherId, cancellationToken);

        if (voucher is null)
        {
            return Result.Failure<PaymentVoucher>(PaymentVoucherErrors.NotFound(paymentVoucherId));
        }

        Result access = await branchAccess.EnsureAccessAsync(voucher.BranchId, cancellationToken);

        return access.IsSuccess ? voucher : Result.Failure<PaymentVoucher>(access.Error);
    }
}

internal sealed class CreatePaymentVoucherCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator) : ICommandHandler<CreatePaymentVoucherCommand, CreatePaymentVoucherResponse>
{
    private const string DocumentPrefix = "PV";

    public async Task<Result<CreatePaymentVoucherResponse>> Handle(CreatePaymentVoucherCommand command, CancellationToken cancellationToken)
    {
        CashBankAccount? cashBank = await context.CashBankAccounts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == command.CashBankAccountId, cancellationToken);

        if (cashBank is null)
        {
            return Result.Failure<CreatePaymentVoucherResponse>(CashBankErrors.NotFound(command.CashBankAccountId));
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

        if (!await context.Vendors.AnyAsync(v => v.Id == command.VendorId, cancellationToken))
        {
            return Result.Failure<CreatePaymentVoucherResponse>(VendorErrors.NotFound(command.VendorId));
        }

        var invoiceIds = command.Allocations.Select(a => a.VendorInvoiceId).Distinct().ToList();
        List<VendorInvoice> invoices = await context.VendorInvoices.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        Guid missing = invoiceIds.Find(id => invoices.TrueForAll(i => i.Id != id));
        if (missing != Guid.Empty)
        {
            return Result.Failure<CreatePaymentVoucherResponse>(VendorInvoiceErrors.NotFound(missing));
        }

        List<(VendorInvoice Invoice, Money Amount)> allocations =
            [.. command.Allocations.Select(a => (invoices.Single(i => i.Id == a.VendorInvoiceId), new Money(a.Amount)))];

        // Validated with a placeholder number first so a rejected voucher does not consume a document number.
        Result<PaymentVoucher> validation = PaymentVoucher.Create(
            string.Empty, cashBank.BranchId, command.VendorId, cashBank.Id, command.PaymentDate, command.Reference, command.Notes, allocations);

        if (validation.IsFailure)
        {
            return Result.Failure<CreatePaymentVoucherResponse>(validation.Error);
        }

        string number = await numberGenerator.NextForBranchAsync(
            context, DocumentPrefix, cashBank.BranchId, command.PaymentDate, cancellationToken);

        PaymentVoucher voucher = PaymentVoucher.Create(
            number, cashBank.BranchId, command.VendorId, cashBank.Id, command.PaymentDate, command.Reference, command.Notes, allocations).Value;

        context.PaymentVouchers.Add(voucher);

        await context.SaveChangesAsync(cancellationToken);

        return new CreatePaymentVoucherResponse(voucher.Id, voucher.Number, voucher.Amount.Amount);
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

        var invoiceIds = voucher.Value.Allocations.Select(a => a.VendorInvoiceId).ToList();
        List<VendorInvoice> invoices = await context.VendorInvoices
            .Where(i => invoiceIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        Result paid = voucher.Value.Pay(command.PaymentDate, invoices, userContext.UserId, dateTimeProvider.UtcNow);
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
/// Journals a paid voucher: Dr hutang usaha / Cr the cash/bank account of the voucher.
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

        string vendor = await context.Vendors
            .Where(v => v.Id == voucher.VendorId)
            .Select(v => v.Name)
            .SingleAsync(cancellationToken);

        await InventoryAccounting.PostAsync(autoJournal, new AccountingEntry(
            AccountingEvents.VendorPayment,
            voucher.Id,
            voucher.BranchId,
            voucher.PaymentDate,
            $"Pembayaran {voucher.Number} kepada {vendor}",
            [new AccountingAmount("Paid", voucher.Amount, CreditAccountId: cashAccountId)]),
            cancellationToken);
    }
}
