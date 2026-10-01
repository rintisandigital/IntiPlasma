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
        Result<Domain.Finance.FiscalPeriods.FiscalPeriod> period = await JournalSupport.FindPeriodAsync(context, command.Date, cancellationToken);
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
