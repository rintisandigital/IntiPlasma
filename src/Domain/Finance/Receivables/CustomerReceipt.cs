using Domain.Sales.SalesInvoices;
using SharedKernel;

namespace Domain.Finance.Receivables;

/// <summary>
/// Penerimaan pembayaran customer into a cash or bank account, allocated to one or more posted invoices of the
/// customer in the same branch. Partial payment is allowed; an allocation can never exceed the invoice's outstanding.
/// Posting raises the event that journals Dr kas/bank / Cr piutang usaha.
/// </summary>
public sealed class CustomerReceipt : AggregateRoot
{
    private readonly List<CustomerReceiptAllocation> _allocations = [];

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
    /// The cash or bank account (chart of accounts) the money was received into; debited by the journal.
    /// </summary>
    public Guid CashAccountId { get; private set; }

    public Money Amount { get; private set; } = Money.Zero;

    /// <summary>
    /// Transfer reference, giro number, etc.
    /// </summary>
    public string? Reference { get; private set; }

    public string? Notes { get; private set; }
    public IReadOnlyCollection<CustomerReceiptAllocation> Allocations => [.. _allocations];

    /// <summary>
    /// Creates the receipt. Does not change the invoices: the caller registers each allocation on its invoice
    /// afterwards (so it can validate first with a placeholder number).
    /// </summary>
    public static Result<CustomerReceipt> Create(
        string number,
        Guid branchId,
        Guid customerId,
        DateOnly receiptDate,
        Guid cashAccountId,
        string? reference,
        string? notes,
        IReadOnlyList<(SalesInvoice Invoice, Money Amount)> allocations)
    {
        Result validation = Validate(branchId, customerId, receiptDate, allocations);
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
            CashAccountId = cashAccountId,
            Reference = reference,
            Notes = notes,
            Amount = allocations.Aggregate(Money.Zero, (total, a) => total + a.Amount)
        };

        receipt._allocations.AddRange(allocations.Select(a => new CustomerReceiptAllocation(receipt.Id, a.Invoice.Id, a.Amount)));

        receipt.Raise(new CustomerReceiptPostedDomainEvent(receipt.Id));

        return receipt;
    }

    private static Result Validate(
        Guid branchId,
        Guid customerId,
        DateOnly receiptDate,
        IReadOnlyList<(SalesInvoice Invoice, Money Amount)> allocations)
    {
        if (allocations.Count == 0)
        {
            return Result.Failure(CustomerReceiptErrors.NoAllocations);
        }

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

            if (receiptDate < invoice.InvoiceDate)
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
        Amount = amount;
    }

    private CustomerReceiptAllocation()
    {
    }

    public Guid CustomerReceiptId { get; private set; }
    public Guid SalesInvoiceId { get; private set; }
    public Money Amount { get; private set; }
}

public sealed record CustomerReceiptPostedDomainEvent(Guid CustomerReceiptId) : DomainEvent;
