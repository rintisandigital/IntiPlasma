using System.Globalization;
using Domain.Sales.SalesInvoices;
using SharedKernel;

namespace Domain.Sales.CreditNotes;

/// <summary>
/// Nota kredit / retur penjualan on a posted sales invoice: reduces invoice lines (excluding VAT) for a price or weight
/// correction, with the VAT reduced proportionally at the line's rate. It lowers the invoice's outstanding (never
/// below zero) and journals Dr potongan &amp; retur penjualan and Dr PPN keluaran / Cr piutang usaha.
/// Posted on creation; a void gives the credit back to the invoice and reverses the journal on the void date.
/// </summary>
public sealed class SalesCreditNote : AggregateRoot
{
    private readonly List<SalesCreditNoteLine> _lines = [];

    private SalesCreditNote(Guid id)
        : base(id)
    {
    }

    private SalesCreditNote()
    {
    }

    public string Number { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid SalesInvoiceId { get; private set; }
    public DateOnly Date { get; private set; }
    public string Reason { get; private set; }
    public Money Subtotal { get; private set; } = new(0m);
    public Money VatAmount { get; private set; } = new(0m);
    public Money Total { get; private set; } = new(0m);
    public SalesCreditNoteStatus Status { get; private set; }
    public DateOnly? VoidDate { get; private set; }
    public string? VoidReason { get; private set; }
    public IReadOnlyCollection<SalesCreditNoteLine> Lines => [.. _lines];

    /// <summary>
    /// Creates the credit note. Does not change the invoice: the caller applies it afterwards
    /// (so it can validate first with a placeholder number).
    /// </summary>
    public static Result<SalesCreditNote> Create(
        string number,
        SalesInvoice invoice,
        DateOnly date,
        string reason,
        IReadOnlyList<(int InvoiceLineNumber, Money Amount)> lines)
    {
        if (!invoice.IsPayable)
        {
            return Result.Failure<SalesCreditNote>(SalesCreditNoteErrors.InvoiceNotOpen(invoice.Id));
        }

        if (date < invoice.InvoiceDate)
        {
            return Result.Failure<SalesCreditNote>(SalesCreditNoteErrors.BeforeInvoiceDate);
        }

        if (lines.Count == 0 || lines.GroupBy(l => l.InvoiceLineNumber).Any(g => g.Count() > 1))
        {
            return Result.Failure<SalesCreditNote>(SalesCreditNoteErrors.InvalidLines);
        }

        var creditNote = new SalesCreditNote(Guid.CreateVersion7())
        {
            Number = number,
            BranchId = invoice.BranchId,
            CustomerId = invoice.CustomerId,
            SalesInvoiceId = invoice.Id,
            Date = date,
            Reason = reason.Trim(),
            Status = SalesCreditNoteStatus.Posted
        };

        foreach ((int lineNumber, Money amount) in lines)
        {
            SalesInvoiceLine? invoiceLine = invoice.Lines.FirstOrDefault(l => l.LineNumber == lineNumber);
            if (invoiceLine is null)
            {
                return Result.Failure<SalesCreditNote>(SalesCreditNoteErrors.InvoiceLineNotFound(lineNumber));
            }

            if (amount.IsNegative || amount.IsZero || amount > invoiceLine.CreditableAmount)
            {
                return Result.Failure<SalesCreditNote>(SalesCreditNoteErrors.AboveCreditable(lineNumber, invoiceLine.CreditableAmount));
            }

            Money vat = invoiceLine.VatAmount * (amount.Amount / invoiceLine.Amount.Amount);

            creditNote._lines.Add(new SalesCreditNoteLine(creditNote.Id, lineNumber, invoiceLine.CycleId, amount, vat));
        }

        creditNote.Subtotal = creditNote._lines.Aggregate(new Money(0m), (total, l) => total + l.Amount);
        creditNote.VatAmount = creditNote._lines.Aggregate(new Money(0m), (total, l) => total + l.VatAmount);
        creditNote.Total = creditNote.Subtotal + creditNote.VatAmount;

        if (creditNote.Total > invoice.Outstanding)
        {
            return Result.Failure<SalesCreditNote>(SalesCreditNoteErrors.AboveOutstanding(invoice.Outstanding));
        }

        creditNote.Raise(new SalesCreditNotePostedDomainEvent(creditNote.Id));

        return creditNote;
    }

    /// <summary>
    /// Voids the credit note: the credited amounts go back to the invoice (its outstanding grows again) and the
    /// journal is reversed on the void date.
    /// </summary>
    /// <param name="invoice">The credited invoice (tracked).</param>
    public Result Void(DateOnly date, string reason, SalesInvoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (Status != SalesCreditNoteStatus.Posted)
        {
            return Result.Failure(SalesCreditNoteErrors.Voided(Id));
        }

        if (date < Date)
        {
            return Result.Failure(SalesCreditNoteErrors.VoidBeforeDate);
        }

        Result reversed = invoice.ReverseCreditNote([.. _lines.Select(l => (l.InvoiceLineNumber, l.Amount))], Total);
        if (reversed.IsFailure)
        {
            return reversed;
        }

        Status = SalesCreditNoteStatus.Voided;
        VoidDate = date;
        VoidReason = reason.Trim();

        Raise(new SalesCreditNoteVoidedDomainEvent(Id));

        return Result.Success();
    }
}

public enum SalesCreditNoteStatus
{
    Posted = 1,
    Voided = 9
}

public sealed class SalesCreditNoteLine
{
    internal SalesCreditNoteLine(Guid salesCreditNoteId, int invoiceLineNumber, Guid cycleId, Money amount, Money vatAmount)
    {
        SalesCreditNoteId = salesCreditNoteId;
        InvoiceLineNumber = invoiceLineNumber;
        CycleId = cycleId;
        Amount = amount with { };
        VatAmount = vatAmount with { };
    }

    private SalesCreditNoteLine()
    {
    }

    public Guid SalesCreditNoteId { get; private set; }
    public int InvoiceLineNumber { get; private set; }

    /// <summary>
    /// The cycle of the invoice line (for profitability per cycle).
    /// </summary>
    public Guid CycleId { get; private set; }

    /// <summary>
    /// Reduction excluding VAT.
    /// </summary>
    public Money Amount { get; private set; }

    public Money VatAmount { get; private set; }
}

public sealed record SalesCreditNotePostedDomainEvent(Guid SalesCreditNoteId) : DomainEvent;

public sealed record SalesCreditNoteVoidedDomainEvent(Guid SalesCreditNoteId) : DomainEvent;

public static class SalesCreditNoteErrors
{
    public static Error NotFound(Guid salesCreditNoteId) => Error.NotFound(
        "SalesCreditNotes.NotFound",
        $"The credit note with the Id = '{salesCreditNoteId}' was not found");

    public static Error InvoiceNotOpen(Guid salesInvoiceId) => Error.Problem(
        "SalesCreditNotes.InvoiceNotOpen",
        $"The sales invoice with the Id = '{salesInvoiceId}' is not posted or already settled");

    public static Error InvoiceLineNotFound(int lineNumber) => Error.Problem(
        "SalesCreditNotes.InvoiceLineNotFound",
        $"The invoice has no line {lineNumber}");

    public static Error AboveCreditable(int lineNumber, Money creditable) => Error.Problem(
        "SalesCreditNotes.AboveCreditable",
        string.Create(CultureInfo.InvariantCulture, $"Invoice line {lineNumber} can be credited by a positive amount of at most {creditable}"));

    public static Error AboveOutstanding(Money outstanding) => Error.Problem(
        "SalesCreditNotes.AboveOutstanding",
        string.Create(CultureInfo.InvariantCulture, $"The credit note (VAT included) cannot exceed the invoice's outstanding {outstanding}"));

    public static readonly Error BeforeInvoiceDate = Error.Problem(
        "SalesCreditNotes.BeforeInvoiceDate",
        "The credit note date cannot be before the invoice date");

    public static Error Voided(Guid salesCreditNoteId) => Error.Problem(
        "SalesCreditNotes.Voided",
        $"The credit note with the Id = '{salesCreditNoteId}' is voided");

    public static readonly Error VoidBeforeDate = Error.Problem(
        "SalesCreditNotes.VoidBeforeDate",
        "A credit note cannot be voided before its date");

    public static readonly Error InvalidLines = Error.Problem(
        "SalesCreditNotes.InvalidLines",
        "A credit note needs at least one line, each invoice line at most once");
}
