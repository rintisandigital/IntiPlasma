using Domain.Common;
using Domain.Sales.SalesInvoices;
using SharedKernel;

namespace Domain.Finance.Receivables;

/// <summary>
/// Penerimaan pembayaran customer into a cash/bank account, allocated to posted invoices of the customer in the same
/// branch; whatever is not allocated is kept as an advance (uang muka penjualan) and applied to invoices later.
/// Partial payment is allowed; an allocation can never exceed the invoice's outstanding.
/// Posting journals Dr kas/bank / Cr piutang usaha (allocated) and Cr uang muka penjualan (advance).
/// A receipt can be voided (reversal journal) as long as none of its advance has been applied.
/// </summary>
public sealed class CustomerReceipt : AggregateRoot, IHasDocuments
{
    private readonly List<CustomerReceiptAllocation> _allocations = [];
    private readonly List<CustomerAdvanceApplication> _applications = [];

    private CustomerReceipt(Guid id)
        : base(id)
    {
    }

    private CustomerReceipt()
    {
    }

    public string Number { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid CustomerId { get; private set; }
    public DateOnly ReceiptDate { get; private set; }

    /// <summary>
    /// The cash/bank account the money was received into. Null only for receipts recorded before cash/bank accounts
    /// existed (phase 5).
    /// </summary>
    public Guid? CashBankAccountId { get; private set; }

    /// <summary>
    /// The chart of accounts account of the cash/bank account; debited by the journal.
    /// </summary>
    public Guid CashAccountId { get; private set; }

    /// <summary>
    /// Total received: allocations + advance.
    /// </summary>
    public Money Amount { get; private set; } = new(0m);

    /// <summary>
    /// Part of the receipt not allocated to an invoice on receipt (uang muka penjualan).
    /// </summary>
    public Money AdvanceAmount { get; private set; } = new(0m);

    public Money AppliedAdvanceAmount { get; private set; } = new(0m);

    public CustomerReceiptStatus Status { get; private set; }

    /// <summary>
    /// Transfer reference, giro number, etc.
    /// </summary>
    public string? Reference { get; private set; }

    public string? Notes { get; private set; }
    public DateOnly? VoidDate { get; private set; }
    public string? VoidReason { get; private set; }
    public IReadOnlyCollection<CustomerReceiptAllocation> Allocations => [.. _allocations];
    public IReadOnlyCollection<CustomerAdvanceApplication> Applications => [.. _applications];

    public Money UnappliedAdvance => Status == CustomerReceiptStatus.Voided ? Money.Zero : AdvanceAmount - AppliedAdvanceAmount;

    /// <summary>
    /// Lampiran: ids of the attached photos and documents.
    /// </summary>
    public Guid[] Documents { get; private set; } = [];

    public Result SetDocuments(IEnumerable<Guid>? documents) =>
        Status == CustomerReceiptStatus.Voided
            ? Result.Failure(DocumentErrors.OwnerCancelled)
            : DocumentList.Apply(documents, value => Documents = value);

    /// <summary>
    /// Creates the receipt. Does not change the invoices: the caller registers each allocation on its invoice
    /// afterwards (so it can validate first with a placeholder number).
    /// </summary>
    public static Result<CustomerReceipt> Create(
        string number,
        Guid branchId,
        Guid customerId,
        DateOnly receiptDate,
        Guid? cashBankAccountId,
        Guid cashAccountId,
        string? reference,
        string? notes,
        IReadOnlyList<(SalesInvoice Invoice, Money Amount)> allocations,
        Money advanceAmount)
    {
        if (advanceAmount.IsNegative || allocations.Count == 0 && advanceAmount.IsZero)
        {
            return Result.Failure<CustomerReceipt>(CustomerReceiptErrors.NoAllocations);
        }

        Result validation = ValidateAllocations(branchId, customerId, receiptDate, allocations);
        if (validation.IsFailure)
        {
            return Result.Failure<CustomerReceipt>(validation.Error);
        }

        var receipt = new CustomerReceipt(Guid.CreateVersion7())
        {
            Number = number,
            BranchId = branchId,
            CustomerId = customerId,
            ReceiptDate = receiptDate,
            CashBankAccountId = cashBankAccountId,
            CashAccountId = cashAccountId,
            Reference = reference,
            Notes = notes,
            Status = CustomerReceiptStatus.Posted,
            AdvanceAmount = advanceAmount with { },
            Amount = allocations.Aggregate(advanceAmount, (total, a) => total + a.Amount)
        };

        receipt._allocations.AddRange(allocations.Select(a => new CustomerReceiptAllocation(receipt.Id, a.Invoice.Id, a.Amount)));

        receipt.Raise(new CustomerReceiptPostedDomainEvent(receipt.Id));

        return receipt;
    }

    /// <summary>
    /// Applies (part of) the advance to posted invoices and registers the payments on them.
    /// Each application journals Dr uang muka penjualan / Cr piutang usaha.
    /// </summary>
    /// <param name="invoices">The invoices to apply to (tracked).</param>
    public Result ApplyAdvance(DateOnly date, IReadOnlyList<(SalesInvoice Invoice, Money Amount)> allocations)
    {
        if (Status != CustomerReceiptStatus.Posted)
        {
            return Result.Failure(CustomerReceiptErrors.Voided(Id));
        }

        if (allocations.Count == 0)
        {
            return Result.Failure(CustomerReceiptErrors.NoAllocations);
        }

        if (date < ReceiptDate)
        {
            return Result.Failure(CustomerReceiptErrors.ApplicationBeforeReceipt);
        }

        Money total = allocations.Aggregate(Money.Zero, (sum, a) => sum + a.Amount);
        if (total > UnappliedAdvance)
        {
            return Result.Failure(CustomerReceiptErrors.AdvanceExceeded(UnappliedAdvance));
        }

        Result validation = ValidateAllocations(BranchId, CustomerId, date, allocations);
        if (validation.IsFailure)
        {
            return validation;
        }

        foreach ((SalesInvoice invoice, Money amount) in allocations)
        {
            invoice.RegisterPayment(amount);

            var application = new CustomerAdvanceApplication(Guid.CreateVersion7(), Id, invoice.Id, date, amount);
            _applications.Add(application);

            Raise(new CustomerAdvanceAppliedDomainEvent(Id, application.Id));
        }

        AppliedAdvanceAmount += total;

        return Result.Success();
    }

    /// <summary>
    /// Voids the receipt: takes the allocations back from the invoices; the journal is reversed on the void date.
    /// </summary>
    /// <param name="invoices">The invoices of the allocations (tracked).</param>
    public Result Void(DateOnly date, string reason, IReadOnlyList<SalesInvoice> invoices)
    {
        if (Status != CustomerReceiptStatus.Posted)
        {
            return Result.Failure(CustomerReceiptErrors.Voided(Id));
        }

        if (_applications.Count > 0)
        {
            return Result.Failure(CustomerReceiptErrors.AdvanceAlreadyApplied);
        }

        if (date < ReceiptDate)
        {
            return Result.Failure(CustomerReceiptErrors.VoidBeforeReceipt);
        }

        foreach (CustomerReceiptAllocation allocation in _allocations)
        {
            Result reversed = invoices.Single(i => i.Id == allocation.SalesInvoiceId).ReversePayment(allocation.Amount);
            if (reversed.IsFailure)
            {
                return reversed;
            }
        }

        Status = CustomerReceiptStatus.Voided;
        VoidDate = date;
        VoidReason = reason;

        Raise(new CustomerReceiptVoidedDomainEvent(Id));

        return Result.Success();
    }

    private static Result ValidateAllocations(
        Guid branchId,
        Guid customerId,
        DateOnly date,
        IReadOnlyList<(SalesInvoice Invoice, Money Amount)> allocations)
    {
        if (allocations.GroupBy(a => a.Invoice.Id).Any(g => g.Count() > 1))
        {
            return Result.Failure(CustomerReceiptErrors.DuplicateInvoice);
        }

        foreach ((SalesInvoice invoice, Money amount) in allocations)
        {
            if (invoice.BranchId != branchId || invoice.CustomerId != customerId)
            {
                return Result.Failure(CustomerReceiptErrors.InvoiceMismatch(invoice.Id));
            }

            if (!invoice.IsPayable)
            {
                return Result.Failure(SalesInvoiceErrors.NotPayable(invoice.Id));
            }

            if (amount.IsNegative || amount.IsZero || amount > invoice.Outstanding)
            {
                return Result.Failure(SalesInvoiceErrors.OverPayment(invoice.Number!, invoice.Outstanding));
            }

            if (date < invoice.InvoiceDate)
            {
                return Result.Failure(CustomerReceiptErrors.BeforeInvoiceDate(invoice.Number!));
            }
        }

        return Result.Success();
    }
}

public sealed class CustomerReceiptAllocation
{
    internal CustomerReceiptAllocation(Guid customerReceiptId, Guid salesInvoiceId, Money amount)
    {
        CustomerReceiptId = customerReceiptId;
        SalesInvoiceId = salesInvoiceId;
        Amount = amount with { };
    }

    private CustomerReceiptAllocation()
    {
    }

    public Guid CustomerReceiptId { get; private set; }
    public Guid SalesInvoiceId { get; private set; }
    public Money Amount { get; private set; }
}

/// <summary>
/// Part of a receipt's advance applied to an invoice on a date.
/// </summary>
public sealed class CustomerAdvanceApplication
{
    internal CustomerAdvanceApplication(Guid id, Guid customerReceiptId, Guid salesInvoiceId, DateOnly date, Money amount)
    {
        Id = id;
        CustomerReceiptId = customerReceiptId;
        SalesInvoiceId = salesInvoiceId;
        Date = date;
        Amount = amount with { };
    }

    private CustomerAdvanceApplication()
    {
    }

    public Guid Id { get; private set; }
    public Guid CustomerReceiptId { get; private set; }
    public Guid SalesInvoiceId { get; private set; }
    public DateOnly Date { get; private set; }
    public Money Amount { get; private set; }
}

public enum CustomerReceiptStatus
{
    Posted = 1,
    Voided = 9
}

public sealed record CustomerReceiptPostedDomainEvent(Guid CustomerReceiptId) : DomainEvent;

public sealed record CustomerReceiptVoidedDomainEvent(Guid CustomerReceiptId) : DomainEvent;

public sealed record CustomerAdvanceAppliedDomainEvent(Guid CustomerReceiptId, Guid ApplicationId) : DomainEvent;
