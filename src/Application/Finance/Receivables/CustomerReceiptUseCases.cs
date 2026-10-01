using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Application.Documents;
using Application.Finance.AutoJournal;
using Application.Finance.CashBank;
using Application.Finance.Journals;
using Application.Inventory;
using Application.Production;
using Domain.Documents.Attachments;
using Domain.Finance.CashBank;
using Domain.Finance.JournalMappings;
using Domain.Finance.Receivables;
using Domain.MasterData.Customers;
using Domain.Sales.SalesInvoices;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Receivables;

public sealed record ReceiptAllocationRequest(Guid SalesInvoiceId, decimal Amount);

/// <summary>
/// Penerimaan pembayaran customer into a cash/bank account of the branch, allocated to posted invoices of the
/// customer; <paramref name="AdvanceAmount"/> is received as uang muka penjualan to apply to invoices later.
/// </summary>
public sealed record CreateCustomerReceiptCommand(
    Guid CashBankAccountId,
    Guid CustomerId,
    DateOnly ReceiptDate,
    string? Reference,
    string? Notes,
    IReadOnlyList<ReceiptAllocationRequest> Allocations,
    decimal AdvanceAmount = 0,
    IReadOnlyList<Guid>? Documents = null) : ICommand<CreateCustomerReceiptResponse>;

public sealed record CreateCustomerReceiptResponse(Guid Id, string Number, decimal Amount);

/// <summary>
/// Applies (part of) a receipt's advance to posted invoices of the customer.
/// </summary>
public sealed record ApplyCustomerAdvanceCommand(
    Guid CustomerReceiptId,
    DateOnly Date,
    IReadOnlyList<ReceiptAllocationRequest> Allocations) : ICommand;

/// <summary>
/// Voids a receipt (e.g. a bounced giro): its allocations are taken back from the invoices and its journal is reversed
/// on the void date.
/// </summary>
public sealed record VoidCustomerReceiptCommand(Guid CustomerReceiptId, DateOnly Date, string Reason) : ICommand;

internal sealed class ReceiptAllocationRequestValidator : AbstractValidator<ReceiptAllocationRequest>
{
    public ReceiptAllocationRequestValidator()
    {
        RuleFor(a => a.SalesInvoiceId).NotEmpty();
        RuleFor(a => a.Amount).GreaterThan(0);
    }
}

internal sealed class CreateCustomerReceiptCommandValidator : AbstractValidator<CreateCustomerReceiptCommand>
{
    public CreateCustomerReceiptCommandValidator()
    {
        RuleFor(c => c.CashBankAccountId).NotEmpty();
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.Reference).MaximumLength(100);
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.AdvanceAmount).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Allocations).NotEmpty().When(c => c.AdvanceAmount == 0);
        RuleForEach(c => c.Allocations).SetValidator(new ReceiptAllocationRequestValidator());
        RuleFor(c => c.Documents).ValidDocuments();
    }
}

internal sealed class ApplyCustomerAdvanceCommandValidator : AbstractValidator<ApplyCustomerAdvanceCommand>
{
    public ApplyCustomerAdvanceCommandValidator()
    {
        RuleFor(c => c.CustomerReceiptId).NotEmpty();
        RuleFor(c => c.Allocations).NotEmpty();
        RuleForEach(c => c.Allocations).SetValidator(new ReceiptAllocationRequestValidator());
    }
}

internal sealed class VoidCustomerReceiptCommandValidator : AbstractValidator<VoidCustomerReceiptCommand>
{
    public VoidCustomerReceiptCommandValidator()
    {
        RuleFor(c => c.CustomerReceiptId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    }
}

internal static class CustomerReceiptSupport
{
    public static async Task<Result<List<(SalesInvoice Invoice, Money Amount)>>> LoadAllocationsAsync(
        IApplicationDbContext context,
        IReadOnlyList<ReceiptAllocationRequest> requests,
        CancellationToken cancellationToken)
    {
        var invoiceIds = requests.Select(a => a.SalesInvoiceId).Distinct().ToList();
        List<SalesInvoice> invoices = await context.SalesInvoices
            .Where(i => invoiceIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        Guid missing = invoiceIds.Find(id => invoices.TrueForAll(i => i.Id != id));
        if (missing != Guid.Empty)
        {
            return Result.Failure<List<(SalesInvoice, Money)>>(SalesInvoiceErrors.NotFound(missing));
        }

        return requests.Select(a => (invoices.Single(i => i.Id == a.SalesInvoiceId), new Money(a.Amount))).ToList();
    }

    public static async Task<Result<CustomerReceipt>> LoadAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid customerReceiptId,
        CancellationToken cancellationToken)
    {
        CustomerReceipt? receipt = await context.CustomerReceipts
            .Include(r => r.Allocations)
            .Include(r => r.Applications)
            .SingleOrDefaultAsync(r => r.Id == customerReceiptId, cancellationToken);

        if (receipt is null)
        {
            return Result.Failure<CustomerReceipt>(CustomerReceiptErrors.NotFound(customerReceiptId));
        }

        Result access = await branchAccess.EnsureAccessAsync(receipt.BranchId, cancellationToken);

        return access.IsSuccess ? receipt : Result.Failure<CustomerReceipt>(access.Error);
    }
}

internal sealed class CreateCustomerReceiptCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator,
    IDateTimeProvider dateTimeProvider,
    IAttachmentService attachments) : ICommandHandler<CreateCustomerReceiptCommand, CreateCustomerReceiptResponse>
{
    private const string DocumentPrefix = "RCV";

    public async Task<Result<CreateCustomerReceiptResponse>> Handle(CreateCustomerReceiptCommand command, CancellationToken cancellationToken)
    {
        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.ReceiptDate, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return Result.Failure<CreateCustomerReceiptResponse>(notFuture.Error);
        }

        CashBankAccount? cashBank = await context.CashBankAccounts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == command.CashBankAccountId, cancellationToken);

        if (cashBank is null)
        {
            return Result.Failure<CreateCustomerReceiptResponse>(CashBankErrors.NotFound(command.CashBankAccountId));
        }

        Result access = await branchAccess.EnsureAccessAsync(cashBank.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CreateCustomerReceiptResponse>(access.Error);
        }

        if (!cashBank.IsActive)
        {
            return Result.Failure<CreateCustomerReceiptResponse>(CashBankErrors.Unusable(cashBank.Id));
        }

        if (!await context.Customers.AnyAsync(c => c.Id == command.CustomerId, cancellationToken))
        {
            return Result.Failure<CreateCustomerReceiptResponse>(CustomerErrors.NotFound(command.CustomerId));
        }

        Result<List<(SalesInvoice Invoice, Money Amount)>> allocations =
            await CustomerReceiptSupport.LoadAllocationsAsync(context, command.Allocations, cancellationToken);

        if (allocations.IsFailure)
        {
            return Result.Failure<CreateCustomerReceiptResponse>(allocations.Error);
        }

        var advance = new Money(command.AdvanceAmount);

        // Validated with a placeholder number first so a rejected receipt does not consume a document number.
        Result<CustomerReceipt> validation = CustomerReceipt.Create(
            string.Empty, cashBank.BranchId, command.CustomerId, command.ReceiptDate, cashBank.Id, cashBank.AccountId,
            command.Reference, command.Notes, allocations.Value, advance);

        if (validation.IsFailure)
        {
            return Result.Failure<CreateCustomerReceiptResponse>(validation.Error);
        }

        Result attachable = await attachments.EnsureAttachableAsync(cashBank.BranchId, command.Documents, cancellationToken);
        if (attachable.IsFailure)
        {
            return Result.Failure<CreateCustomerReceiptResponse>(attachable.Error);
        }

        string number = await numberGenerator.NextForBranchAsync(
            context, DocumentPrefix, cashBank.BranchId, command.ReceiptDate, cancellationToken);

        CustomerReceipt receipt = CustomerReceipt.Create(
            number, cashBank.BranchId, command.CustomerId, command.ReceiptDate, cashBank.Id, cashBank.AccountId,
            command.Reference, command.Notes, allocations.Value, advance).Value;

        foreach ((SalesInvoice invoice, Money amount) in allocations.Value)
        {
            Result paid = invoice.RegisterPayment(amount);
            if (paid.IsFailure)
            {
                return Result.Failure<CreateCustomerReceiptResponse>(paid.Error);
            }
        }

        Result documents = await attachments.ApplyDocumentsAsync(
            receipt,
            AttachmentOwner.Of(AttachmentOwnerTypes.CustomerReceipt, receipt.Id),
            receipt.BranchId,
            command.Documents,
            cancellationToken);
        if (documents.IsFailure)
        {
            return Result.Failure<CreateCustomerReceiptResponse>(documents.Error);
        }

        context.CustomerReceipts.Add(receipt);

        await context.SaveChangesAsync(cancellationToken);

        return new CreateCustomerReceiptResponse(receipt.Id, receipt.Number, receipt.Amount.Amount);
    }
}

internal sealed class ApplyCustomerAdvanceCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ApplyCustomerAdvanceCommand>
{
    public async Task<Result> Handle(ApplyCustomerAdvanceCommand command, CancellationToken cancellationToken)
    {
        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.Date, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return notFuture;
        }

        Result<CustomerReceipt> receipt = await CustomerReceiptSupport.LoadAsync(
            context, branchAccess, command.CustomerReceiptId, cancellationToken);

        if (receipt.IsFailure)
        {
            return receipt;
        }

        Result<List<(SalesInvoice Invoice, Money Amount)>> allocations =
            await CustomerReceiptSupport.LoadAllocationsAsync(context, command.Allocations, cancellationToken);

        if (allocations.IsFailure)
        {
            return allocations;
        }

        Result applied = receipt.Value.ApplyAdvance(command.Date, allocations.Value);
        if (applied.IsFailure)
        {
            return applied;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class VoidCustomerReceiptCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<VoidCustomerReceiptCommand>
{
    public async Task<Result> Handle(VoidCustomerReceiptCommand command, CancellationToken cancellationToken)
    {
        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.Date, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return notFuture;
        }

        Result<CustomerReceipt> receipt = await CustomerReceiptSupport.LoadAsync(
            context, branchAccess, command.CustomerReceiptId, cancellationToken);

        if (receipt.IsFailure)
        {
            return receipt;
        }

        // The reversal journal is posted from the outbox; reject a closed period now rather than dead-letter it later.
        Result<Domain.Finance.FiscalPeriods.FiscalPeriod> period = await JournalSupport.FindPeriodAsync(context, command.Date, cancellationToken);
        if (period.IsFailure)
        {
            return period;
        }

        var invoiceIds = receipt.Value.Allocations.Select(a => a.SalesInvoiceId).ToList();
        List<SalesInvoice> invoices = await context.SalesInvoices
            .Where(i => invoiceIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        Result voided = receipt.Value.Void(command.Date, command.Reason, invoices);
        if (voided.IsFailure)
        {
            return voided;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>
/// Journals a customer receipt: Dr the cash/bank account / Cr piutang usaha for the allocated part and
/// Cr uang muka penjualan for the advance.
/// </summary>
internal sealed class CustomerReceiptPostedDomainEventHandler(IApplicationDbContext context, IAutoJournalService autoJournal)
    : IDomainEventHandler<CustomerReceiptPostedDomainEvent>
{
    public async Task Handle(CustomerReceiptPostedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        CustomerReceipt receipt = await context.CustomerReceipts.AsNoTracking()
            .SingleAsync(r => r.Id == domainEvent.CustomerReceiptId, cancellationToken);

        string customer = await context.Customers
            .Where(c => c.Id == receipt.CustomerId)
            .Select(c => c.Name)
            .SingleAsync(cancellationToken);

        await InventoryAccounting.PostAsync(autoJournal, new AccountingEntry(
            AccountingEvents.CustomerReceipt,
            receipt.Id,
            receipt.BranchId,
            receipt.ReceiptDate,
            $"Penerimaan {receipt.Number} dari {customer}",
            [
                new AccountingAmount("Received", receipt.Amount - receipt.AdvanceAmount, DebitAccountId: receipt.CashAccountId),
                new AccountingAmount("Advance", receipt.AdvanceAmount, DebitAccountId: receipt.CashAccountId)
            ]),
            cancellationToken);
    }
}

/// <summary>
/// Reverses the journal of a voided customer receipt on the void date.
/// </summary>
internal sealed class CustomerReceiptVoidedDomainEventHandler(IApplicationDbContext context, IAutoJournalService autoJournal)
    : IDomainEventHandler<CustomerReceiptVoidedDomainEvent>
{
    public async Task Handle(CustomerReceiptVoidedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        CustomerReceipt receipt = await context.CustomerReceipts.AsNoTracking()
            .SingleAsync(r => r.Id == domainEvent.CustomerReceiptId, cancellationToken);

        await AutoJournalPosting.EnsureAsync(
            autoJournal.ReverseAsync(
                AccountingEvents.CustomerReceipt, receipt.Id, receipt.VoidDate!.Value, $"void {receipt.Number}: {receipt.VoidReason}", cancellationToken),
            $"void of customer receipt {receipt.Number}");
    }
}

/// <summary>
/// Journals an advance applied to an invoice: Dr uang muka penjualan / Cr piutang usaha.
/// </summary>
internal sealed class CustomerAdvanceAppliedDomainEventHandler(IApplicationDbContext context, IAutoJournalService autoJournal)
    : IDomainEventHandler<CustomerAdvanceAppliedDomainEvent>
{
    public async Task Handle(CustomerAdvanceAppliedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        CustomerReceipt receipt = await context.CustomerReceipts.AsNoTracking()
            .Include(r => r.Applications)
            .SingleAsync(r => r.Id == domainEvent.CustomerReceiptId, cancellationToken);

        CustomerAdvanceApplication application = receipt.Applications.Single(a => a.Id == domainEvent.ApplicationId);

        string invoice = await context.SalesInvoices
            .Where(i => i.Id == application.SalesInvoiceId)
            .Select(i => i.Number!)
            .SingleAsync(cancellationToken);

        await InventoryAccounting.PostAsync(autoJournal, new AccountingEntry(
            AccountingEvents.CustomerAdvanceApplied,
            application.Id,
            receipt.BranchId,
            application.Date,
            $"Uang muka {receipt.Number} untuk {invoice}",
            [new AccountingAmount("Applied", application.Amount)]),
            cancellationToken);
    }
}
