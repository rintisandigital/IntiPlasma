using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Application.Finance.AutoJournal;
using Application.Finance.Journals;
using Application.Inventory;
using Application.Production;
using Domain.Finance.JournalMappings;
using Domain.Sales.CreditNotes;
using Domain.Sales.SalesInvoices;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales;

/// <param name="Amount">Reduction of the invoice line, excluding VAT.</param>
public sealed record CreditNoteLineRequest(int InvoiceLineNumber, decimal Amount);

/// <summary>
/// Nota kredit / retur penjualan on a posted invoice (price or weight correction). Posted immediately.
/// </summary>
public sealed record CreateSalesCreditNoteCommand(
    Guid SalesInvoiceId,
    DateOnly Date,
    string Reason,
    IReadOnlyList<CreditNoteLineRequest> Lines) : ICommand<CreateSalesCreditNoteResponse>;

public sealed record CreateSalesCreditNoteResponse(Guid Id, string Number, decimal Total);

/// <summary>
/// Voids a credit note: the credit goes back to the invoice and the journal is reversed on <paramref name="Date"/>.
/// </summary>
public sealed record VoidSalesCreditNoteCommand(Guid SalesCreditNoteId, DateOnly Date, string Reason) : ICommand;

internal sealed class CreateSalesCreditNoteCommandValidator : AbstractValidator<CreateSalesCreditNoteCommand>
{
    public CreateSalesCreditNoteCommandValidator()
    {
        RuleFor(c => c.SalesInvoiceId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.InvoiceLineNumber).GreaterThan(0);
            line.RuleFor(l => l.Amount).GreaterThan(0);
        });
    }
}

internal sealed class VoidSalesCreditNoteCommandValidator : AbstractValidator<VoidSalesCreditNoteCommand>
{
    public VoidSalesCreditNoteCommandValidator()
    {
        RuleFor(c => c.SalesCreditNoteId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class CreateSalesCreditNoteCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDocumentNumberGenerator numberGenerator,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<CreateSalesCreditNoteCommand, CreateSalesCreditNoteResponse>
{
    private const string DocumentPrefix = "CN";

    public async Task<Result<CreateSalesCreditNoteResponse>> Handle(CreateSalesCreditNoteCommand command, CancellationToken cancellationToken)
    {
        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.Date, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return Result.Failure<CreateSalesCreditNoteResponse>(notFuture.Error);
        }

        Result<SalesInvoice> invoice = await SalesSupport.LoadInvoiceAsync(context, branchAccess, command.SalesInvoiceId, cancellationToken);
        if (invoice.IsFailure)
        {
            return Result.Failure<CreateSalesCreditNoteResponse>(invoice.Error);
        }

        // The journal is posted from the outbox; reject a closed period now rather than dead-letter it later.
        Result period = await JournalSupport.EnsurePeriodOpenAsync(context, command.Date, cancellationToken);
        if (period.IsFailure)
        {
            return Result.Failure<CreateSalesCreditNoteResponse>(period.Error);
        }

        List<(int InvoiceLineNumber, Money Amount)> lines = [.. command.Lines.Select(l => (l.InvoiceLineNumber, new Money(l.Amount)))];

        // Validated with a placeholder number first so a rejected credit note does not consume a document number.
        Result<SalesCreditNote> validation = SalesCreditNote.Create(string.Empty, invoice.Value, command.Date, command.Reason, lines);
        if (validation.IsFailure)
        {
            return Result.Failure<CreateSalesCreditNoteResponse>(validation.Error);
        }

        string number = await numberGenerator.NextForBranchAsync(
            context, DocumentPrefix, invoice.Value.BranchId, command.Date, cancellationToken);

        SalesCreditNote creditNote = SalesCreditNote.Create(number, invoice.Value, command.Date, command.Reason, lines).Value;

        Result applied = invoice.Value.ApplyCreditNote(
            [.. creditNote.Lines.Select(l => (l.InvoiceLineNumber, l.Amount))], creditNote.Total);

        if (applied.IsFailure)
        {
            return Result.Failure<CreateSalesCreditNoteResponse>(applied.Error);
        }

        context.SalesCreditNotes.Add(creditNote);

        await context.SaveChangesAsync(cancellationToken);

        return new CreateSalesCreditNoteResponse(creditNote.Id, creditNote.Number, creditNote.Total.Amount);
    }
}

internal sealed class VoidSalesCreditNoteCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<VoidSalesCreditNoteCommand>
{
    public async Task<Result> Handle(VoidSalesCreditNoteCommand command, CancellationToken cancellationToken)
    {
        Result notFuture = DailyRecordingSupport.EnsureNotFuture(command.Date, dateTimeProvider);
        if (notFuture.IsFailure)
        {
            return notFuture;
        }

        SalesCreditNote? creditNote = await context.SalesCreditNotes
            .Include(n => n.Lines)
            .SingleOrDefaultAsync(n => n.Id == command.SalesCreditNoteId, cancellationToken);

        if (creditNote is null)
        {
            return Result.Failure(SalesCreditNoteErrors.NotFound(command.SalesCreditNoteId));
        }

        Result<SalesInvoice> invoice = await SalesSupport.LoadInvoiceAsync(context, branchAccess, creditNote.SalesInvoiceId, cancellationToken);
        if (invoice.IsFailure)
        {
            return invoice;
        }

        // The reversal journal is posted from the outbox; reject a closed period now rather than dead-letter it later.
        Result period = await JournalSupport.EnsurePeriodOpenAsync(context, command.Date, cancellationToken);
        if (period.IsFailure)
        {
            return period;
        }

        Result voided = creditNote.Void(command.Date, command.Reason, invoice.Value);
        if (voided.IsFailure)
        {
            return voided;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>
/// Journals a credit note: Dr potongan &amp; retur penjualan (DPP) and Dr PPN keluaran / Cr piutang usaha.
/// </summary>
internal sealed class SalesCreditNotePostedDomainEventHandler(IApplicationDbContext context, IAutoJournalService autoJournal)
    : IDomainEventHandler<SalesCreditNotePostedDomainEvent>
{
    public async Task Handle(SalesCreditNotePostedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        SalesCreditNote creditNote = await context.SalesCreditNotes.AsNoTracking()
            .SingleAsync(c => c.Id == domainEvent.SalesCreditNoteId, cancellationToken);

        string invoice = await context.SalesInvoices
            .Where(i => i.Id == creditNote.SalesInvoiceId)
            .Select(i => i.Number!)
            .SingleAsync(cancellationToken);

        await InventoryAccounting.PostAsync(autoJournal, new AccountingEntry(
            AccountingEvents.SalesCreditNote,
            creditNote.Id,
            creditNote.BranchId,
            creditNote.Date,
            $"Nota kredit {creditNote.Number} atas {invoice}: {creditNote.Reason}",
            [
                new AccountingAmount("SalesReturn", creditNote.Subtotal),
                new AccountingAmount("OutputVat", creditNote.VatAmount)
            ]),
            cancellationToken);
    }
}

/// <summary>
/// Reverses the journal of a voided credit note on the void date.
/// </summary>
internal sealed class SalesCreditNoteVoidedDomainEventHandler(IApplicationDbContext context, IAutoJournalService autoJournal)
    : IDomainEventHandler<SalesCreditNoteVoidedDomainEvent>
{
    public async Task Handle(SalesCreditNoteVoidedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        SalesCreditNote creditNote = await context.SalesCreditNotes.AsNoTracking()
            .SingleAsync(n => n.Id == domainEvent.SalesCreditNoteId, cancellationToken);

        await AutoJournalPosting.EnsureAsync(
            autoJournal.ReverseAsync(
                AccountingEvents.SalesCreditNote, creditNote.Id, creditNote.VoidDate!.Value,
                $"void {creditNote.Number}: {creditNote.VoidReason}", cancellationToken),
            $"void of credit note {creditNote.Number}");
    }
}
