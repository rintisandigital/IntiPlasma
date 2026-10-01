using Domain.MasterData.TaxCodes;
using Domain.Sales.DeliveryOrders;
using SharedKernel;

namespace Domain.Sales.SalesInvoices;

/// <summary>
/// Sales invoice (faktur penjualan) billing one or more delivery orders of one customer and branch.
/// Draft → Posted → (Partially) Paid. The number is given when posting, so posted invoices are numbered without gaps;
/// posting raises the event that journals the receivable (piutang / penjualan / PPN keluaran). A draft can be cancelled,
/// which makes its delivery orders billable again; a posted invoice is final.
/// </summary>
public sealed class SalesInvoice : AggregateRoot
{
    private readonly List<SalesInvoiceLine> _lines = [];

    private SalesInvoice(Guid id)
        : base(id)
    {
    }

    private SalesInvoice()
    {
    }

    /// <summary>
    /// Null while the invoice is a draft.
    /// </summary>
    public string? Number { get; private set; }

    public Guid BranchId { get; private set; }
    public Guid CustomerId { get; private set; }
    public DateOnly InvoiceDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public SalesInvoiceStatus Status { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>
    /// Total excluding VAT (DPP before the tax base ratio).
    /// </summary>
    public Money Subtotal { get; private set; } = Money.Zero;

    public Money VatAmount { get; private set; } = Money.Zero;
    public Money Total { get; private set; } = Money.Zero;
    public Money PaidAmount { get; private set; } = Money.Zero;
    public Guid? PostedBy { get; private set; }
    public DateTime? PostedAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public IReadOnlyCollection<SalesInvoiceLine> Lines => [.. _lines];

    /// <summary>
    /// Total of the posted credit notes (nota kredit), VAT included.
    /// </summary>
    public Money CreditedAmount { get; private set; } = new(0m);

    public Money Outstanding => Total - PaidAmount - CreditedAmount;

    public bool IsPayable => Status is SalesInvoiceStatus.Posted or SalesInvoiceStatus.PartiallyPaid;

    /// <summary>
    /// Creates a draft for the deliveries and marks them as invoiced. VAT per line follows the line's VAT code and
    /// the rate in effect on the invoice date; exempt or not-collected VAT codes yield no VAT.
    /// </summary>
    /// <param name="taxCodes">Every VAT code used on the deliveries, with its rates.</param>
    public static Result<SalesInvoice> CreateDraft(
        IReadOnlyList<DeliveryOrder> deliveries,
        DateOnly invoiceDate,
        int paymentTermDays,
        string? notes,
        IReadOnlyDictionary<Guid, TaxCode> taxCodes)
    {
        Result validation = Validate(deliveries, invoiceDate);
        if (validation.IsFailure)
        {
            return Result.Failure<SalesInvoice>(validation.Error);
        }

        var invoice = new SalesInvoice(Guid.CreateVersion7())
        {
            BranchId = deliveries[0].BranchId,
            CustomerId = deliveries[0].CustomerId,
            InvoiceDate = invoiceDate,
            DueDate = invoiceDate.AddDays(paymentTermDays),
            Notes = notes,
            Status = SalesInvoiceStatus.Draft,
            PaidAmount = new Money(0m)
        };

        foreach (DeliveryOrderLine deliveryLine in deliveries.OrderBy(d => d.DeliveryDate).ThenBy(d => d.Number).SelectMany(d => d.Lines))
        {
            Result<TaxCalculation> vat = deliveryLine.TaxCodeId is null
                ? TaxCalculation.None
                : taxCodes[deliveryLine.TaxCodeId.Value].Calculate(deliveryLine.Amount, invoiceDate);

            if (vat.IsFailure)
            {
                return Result.Failure<SalesInvoice>(vat.Error);
            }

            invoice._lines.Add(new SalesInvoiceLine(invoice.Id, invoice._lines.Count + 1, deliveryLine, vat.Value));
        }

        invoice.Subtotal = invoice._lines.Aggregate(Money.Zero, (total, line) => total + line.Amount);
        invoice.VatAmount = invoice._lines.Aggregate(Money.Zero, (total, line) => total + line.VatAmount);
        invoice.Total = invoice.Subtotal + invoice.VatAmount;

        foreach (DeliveryOrder delivery in deliveries)
        {
            delivery.MarkInvoiced(invoice.Id);
        }

        return invoice;
    }

    public Result EnsurePostable() =>
        Status == SalesInvoiceStatus.Draft
            ? Result.Success()
            : Result.Failure(SalesInvoiceErrors.InvalidTransition(Status, SalesInvoiceStatus.Posted));

    public Result Post(string number, Guid? userId, DateTime utcNow)
    {
        Result postable = EnsurePostable();
        if (postable.IsFailure)
        {
            return postable;
        }

        Number = number;
        Status = SalesInvoiceStatus.Posted;
        PostedBy = userId;
        PostedAtUtc = utcNow;

        Raise(new SalesInvoicePostedDomainEvent(Id));

        return Result.Success();
    }

    /// <summary>
    /// Cancels a draft and releases its delivery orders (the ones billed on this invoice).
    /// </summary>
    public Result Cancel(string reason, IReadOnlyList<DeliveryOrder> deliveries)
    {
        if (Status != SalesInvoiceStatus.Draft)
        {
            return Result.Failure(SalesInvoiceErrors.InvalidTransition(Status, SalesInvoiceStatus.Cancelled));
        }

        Status = SalesInvoiceStatus.Cancelled;
        CancellationReason = reason;

        foreach (DeliveryOrder delivery in deliveries.Where(d => d.SalesInvoiceId == Id))
        {
            delivery.ReleaseInvoice();
        }

        return Result.Success();
    }

    /// <summary>
    /// Applies (part of) a customer payment to the invoice.
    /// </summary>
    public Result RegisterPayment(Money amount)
    {
        if (!IsPayable)
        {
            return Result.Failure(SalesInvoiceErrors.NotPayable(Id));
        }

        if (amount.IsNegative || amount.IsZero || amount > Outstanding)
        {
            return Result.Failure(SalesInvoiceErrors.OverPayment(Number!, Outstanding));
        }

        PaidAmount += amount;
        UpdateSettlementStatus();

        return Result.Success();
    }

    /// <summary>
    /// Takes back a payment of a voided customer receipt.
    /// </summary>
    public Result ReversePayment(Money amount)
    {
        if (amount > PaidAmount)
        {
            return Result.Failure(SalesInvoiceErrors.InvalidPaymentReversal(Id));
        }

        PaidAmount -= amount;
        UpdateSettlementStatus();

        return Result.Success();
    }

    /// <summary>
    /// Applies a posted credit note: reduces the credited lines and the outstanding.
    /// </summary>
    public Result ApplyCreditNote(IReadOnlyList<(int LineNumber, Money Amount)> lines, Money total)
    {
        if (!IsPayable)
        {
            return Result.Failure(SalesInvoiceErrors.NotPayable(Id));
        }

        foreach ((int lineNumber, Money amount) in lines)
        {
            _lines.Single(l => l.LineNumber == lineNumber).AddCredit(amount);
        }

        CreditedAmount += total;
        UpdateSettlementStatus();

        return Result.Success();
    }

    private void UpdateSettlementStatus()
    {
        if (Outstanding.IsZero)
        {
            Status = SalesInvoiceStatus.Paid;
            return;
        }

        Status = PaidAmount.IsZero ? SalesInvoiceStatus.Posted : SalesInvoiceStatus.PartiallyPaid;
    }

    private static Result Validate(IReadOnlyList<DeliveryOrder> deliveries, DateOnly invoiceDate)
    {
        if (deliveries.Count == 0)
        {
            return Result.Failure(SalesInvoiceErrors.NoDeliveries);
        }

        if (deliveries.GroupBy(d => d.Id).Any(g => g.Count() > 1))
        {
            return Result.Failure(SalesInvoiceErrors.DuplicateDelivery);
        }

        if (deliveries.Any(d => d.BranchId != deliveries[0].BranchId || d.CustomerId != deliveries[0].CustomerId))
        {
            return Result.Failure(SalesInvoiceErrors.MixedDeliveries);
        }

        DeliveryOrder? notInvoiceable = deliveries.FirstOrDefault(d => d.Status != DeliveryOrderStatus.Delivered);
        if (notInvoiceable is not null)
        {
            return Result.Failure(DeliveryOrderErrors.NotInvoiceable(notInvoiceable.Id));
        }

        return deliveries.Any(d => invoiceDate < d.DeliveryDate)
            ? Result.Failure(SalesInvoiceErrors.BeforeDeliveryDate)
            : Result.Success();
    }
}

public enum SalesInvoiceStatus
{
    Draft = 1,
    Posted = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Cancelled = 9
}
