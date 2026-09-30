using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Application.Finance.AutoJournal;
using Application.Inventory;
using Application.Production;
using Domain.Finance.Accounts;
using Domain.Finance.JournalMappings;
using Domain.Finance.Receivables;
using Domain.MasterData.Branches;
using Domain.MasterData.Customers;
using Domain.Sales.SalesInvoices;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Receivables;

public sealed record ReceiptAllocationRequest(Guid SalesInvoiceId, decimal Amount);

/// <summary>
/// Penerimaan pembayaran customer into a cash/bank account, allocated to posted invoices of the customer in the branch.
/// </summary>
public sealed record CreateCustomerReceiptCommand(
    Guid BranchId,
    Guid CustomerId,
    DateOnly ReceiptDate,
    Guid CashAccountId,
    string? Reference,
    string? Notes,
    IReadOnlyList<ReceiptAllocationRequest> Allocations) : ICommand<CreateCustomerReceiptResponse>;

public sealed record CreateCustomerReceiptResponse(Guid Id, string Number, decimal Amount);

internal sealed class CreateCustomerReceiptCommandValidator : AbstractValidator<CreateCustomerReceiptCommand>
{
    public CreateCustomerReceiptCommandValidator()
    {
        RuleFor(c => c.BranchId).NotEmpty();
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.CashAccountId).NotEmpty();
        RuleFor(c => c.Reference).MaximumLength(100);
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Allocations).NotEmpty();
        RuleForEach(c => c.Allocations).ChildRules(allocation =>
        {
            allocation.RuleFor(a => a.SalesInvoiceId).NotEmpty();
            allocation.RuleFor(a => a.Amount).GreaterThan(0);
        });
    }
}

internal sealed class CreateCustomerReceiptCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<CreateCustomerReceiptCommand, CreateCustomerReceiptResponse>
{
    private const string DocumentPrefix = "RCV";

    public async Task<Result<CreateCustomerReceiptResponse>> Handle(CreateCustomerReceiptCommand command, CancellationToken cancellationToken)
    {
        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.ReceiptDate, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return Result.Failure<CreateCustomerReceiptResponse>(notFuture.Error);
        }

        Result access = await branchAccess.EnsureAccessAsync(command.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CreateCustomerReceiptResponse>(access.Error);
        }

        if (!await context.Branches.AnyAsync(b => b.Id == command.BranchId && b.IsActive, cancellationToken))
        {
            return Result.Failure<CreateCustomerReceiptResponse>(BranchErrors.NotFound(command.BranchId));
        }

        if (!await context.Customers.AnyAsync(c => c.Id == command.CustomerId, cancellationToken))
        {
            return Result.Failure<CreateCustomerReceiptResponse>(CustomerErrors.NotFound(command.CustomerId));
        }

        bool validCashAccount = await context.Accounts.AnyAsync(
            a => a.Id == command.CashAccountId && a.IsActive && a.IsPostable && a.Type == AccountType.Asset,
            cancellationToken);

        if (!validCashAccount)
        {
            return Result.Failure<CreateCustomerReceiptResponse>(CustomerReceiptErrors.InvalidCashAccount);
        }

        var invoiceIds = command.Allocations.Select(a => a.SalesInvoiceId).Distinct().ToList();
        List<SalesInvoice> invoices = await context.SalesInvoices
            .Where(i => invoiceIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        Guid missing = invoiceIds.Find(id => invoices.TrueForAll(i => i.Id != id));
        if (missing != Guid.Empty)
        {
            return Result.Failure<CreateCustomerReceiptResponse>(SalesInvoiceErrors.NotFound(missing));
        }

        List<(SalesInvoice Invoice, Money Amount)> allocations =
        [
            .. command.Allocations.Select(a => (invoices.Single(i => i.Id == a.SalesInvoiceId), new Money(a.Amount)))
        ];

        // Validated with a placeholder number first so a rejected receipt does not consume a document number.
        Result<CustomerReceipt> validation = CustomerReceipt.Create(
            string.Empty, command.BranchId, command.CustomerId, command.ReceiptDate, command.CashAccountId,
            command.Reference, command.Notes, allocations);

        if (validation.IsFailure)
        {
            return Result.Failure<CreateCustomerReceiptResponse>(validation.Error);
        }

        string number = await numberGenerator.NextForBranchAsync(
            context, DocumentPrefix, command.BranchId, command.ReceiptDate, cancellationToken);

        CustomerReceipt receipt = CustomerReceipt.Create(
            number, command.BranchId, command.CustomerId, command.ReceiptDate, command.CashAccountId,
            command.Reference, command.Notes, allocations).Value;

        foreach ((SalesInvoice invoice, Money amount) in allocations)
        {
            Result paid = invoice.RegisterPayment(amount);
            if (paid.IsFailure)
            {
                return Result.Failure<CreateCustomerReceiptResponse>(paid.Error);
            }
        }

        context.CustomerReceipts.Add(receipt);

        await context.SaveChangesAsync(cancellationToken);

        return new CreateCustomerReceiptResponse(receipt.Id, receipt.Number, receipt.Amount.Amount);
    }
}

/// <summary>
/// Journals a customer receipt: Dr the cash/bank account of the receipt / Cr piutang usaha.
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
            [new AccountingAmount("Received", receipt.Amount, DebitAccountId: receipt.CashAccountId)]),
            cancellationToken);
    }
}
