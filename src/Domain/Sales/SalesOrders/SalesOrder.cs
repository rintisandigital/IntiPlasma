using SharedKernel;

namespace Domain.Sales.SalesOrders;

/// <summary>
/// Sales order (kontrak penjualan) of live birds to one customer: ordered birds with an estimated weight and a price
/// per kg. Draft → Approved → (Partially) Delivered → Closed. Delivery orders are registered against the lines;
/// the number of birds delivered can never exceed the ordered birds, the weight is whatever the scale says.
/// </summary>
public sealed class SalesOrder : AggregateRoot
{
    private readonly List<SalesOrderLine> _lines = [];

    private SalesOrder(Guid id, string number, Guid branchId, Guid customerId)
        : base(id)
    {
        Number = number;
        BranchId = branchId;
        CustomerId = customerId;
        Status = SalesOrderStatus.Draft;
    }

    private SalesOrder()
    {
    }

    public string Number { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid CustomerId { get; private set; }
    public DateOnly OrderDate { get; private set; }
    public DateOnly? DeliveryDate { get; private set; }
    public SalesOrderStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }

    /// <summary>
    /// Set when the order was approved above the customer's credit limit, with the reason given by the approver.
    /// </summary>
    public string? CreditOverrideReason { get; private set; }

    public string? CancellationReason { get; private set; }
    public IReadOnlyCollection<SalesOrderLine> Lines => [.. _lines];

    /// <summary>
    /// Estimated order value excluding VAT (estimated weight × price per kg).
    /// </summary>
    public Money EstimatedAmount => _lines.Aggregate(Money.Zero, (total, line) => total + line.EstimatedAmount);

    /// <summary>
    /// Estimated value of the birds not delivered yet; part of the customer's credit exposure while the order is open.
    /// </summary>
    public Money OutstandingEstimatedAmount => IsOpen
        ? _lines.Aggregate(Money.Zero, (total, line) => total + line.OutstandingEstimatedAmount)
        : Money.Zero;

    public bool CanDeliver => Status is SalesOrderStatus.Approved or SalesOrderStatus.PartiallyDelivered;

    public bool IsOpen => Status is SalesOrderStatus.Draft or SalesOrderStatus.Approved or SalesOrderStatus.PartiallyDelivered;

    public static Result<SalesOrder> Create(
        string number,
        Guid branchId,
        Guid customerId,
        DateOnly orderDate,
        DateOnly? deliveryDate,
        string? notes,
        IReadOnlyList<SalesOrderLineInput> lines)
    {
        var order = new SalesOrder(Guid.CreateVersion7(), number, branchId, customerId);

        Result result = order.Apply(orderDate, deliveryDate, notes, lines);

        return result.IsSuccess ? order : Result.Failure<SalesOrder>(result.Error);
    }

    public Result Update(DateOnly orderDate, DateOnly? deliveryDate, string? notes, IReadOnlyList<SalesOrderLineInput> lines)
    {
        if (Status != SalesOrderStatus.Draft)
        {
            return Result.Failure(SalesOrderErrors.NotDraft(Id));
        }

        return Apply(orderDate, deliveryDate, notes, lines);
    }

    /// <summary>
    /// Approves the order after the credit check: the customer's exposure (open receivables, uninvoiced deliveries
    /// and other open orders) plus this order must stay within the credit limit. A limit of zero means no credit.
    /// Exceeding the limit is only allowed with an override reason (a separate permission).
    /// </summary>
    public Result Approve(Guid approverId, DateTime utcNow, CreditCheck creditCheck, string? overrideReason)
    {
        if (Status != SalesOrderStatus.Draft)
        {
            return Result.Failure(SalesOrderErrors.InvalidTransition(Status, SalesOrderStatus.Approved));
        }

        Money required = creditCheck.Exposure + EstimatedAmount;
        bool withinLimit = required <= creditCheck.Limit;

        if (!withinLimit && string.IsNullOrWhiteSpace(overrideReason))
        {
            return Result.Failure(SalesOrderErrors.CreditLimitExceeded(creditCheck.Limit, creditCheck.Exposure, EstimatedAmount));
        }

        Status = SalesOrderStatus.Approved;
        ApprovedBy = approverId;
        ApprovedAtUtc = utcNow;
        CreditOverrideReason = withinLimit ? null : overrideReason!.Trim();

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status is not (SalesOrderStatus.Draft or SalesOrderStatus.Approved))
        {
            return Result.Failure(SalesOrderErrors.InvalidTransition(Status, SalesOrderStatus.Cancelled));
        }

        Status = SalesOrderStatus.Cancelled;
        CancellationReason = reason;

        return Result.Success();
    }

    /// <summary>
    /// Closes a partially delivered order: the remaining birds will not be delivered.
    /// </summary>
    public Result Close()
    {
        if (Status != SalesOrderStatus.PartiallyDelivered)
        {
            return Result.Failure(SalesOrderErrors.InvalidTransition(Status, SalesOrderStatus.Closed));
        }

        Status = SalesOrderStatus.Closed;

        return Result.Success();
    }

    public Result RegisterDelivery(int lineNumber, int birds, decimal weightKg)
    {
        if (!CanDeliver)
        {
            return Result.Failure(SalesOrderErrors.NotDeliverable(Id));
        }

        SalesOrderLine? line = _lines.Find(l => l.LineNumber == lineNumber);
        if (line is null)
        {
            return Result.Failure(SalesOrderErrors.LineNotFound(lineNumber));
        }

        if (birds <= 0 || weightKg <= 0 || birds > line.OutstandingBirds)
        {
            return Result.Failure(SalesOrderErrors.OverDelivery(lineNumber, line.OutstandingBirds));
        }

        line.AddDelivered(birds, weightKg);
        UpdateDeliveryStatus();

        return Result.Success();
    }

    /// <summary>
    /// Takes back a delivery of a cancelled delivery order. A closed order stays closed.
    /// </summary>
    public Result ReverseDelivery(int lineNumber, int birds, decimal weightKg)
    {
        SalesOrderLine? line = _lines.Find(l => l.LineNumber == lineNumber);
        if (line is null)
        {
            return Result.Failure(SalesOrderErrors.LineNotFound(lineNumber));
        }

        if (birds > line.DeliveredBirds || weightKg > line.DeliveredWeightKg)
        {
            return Result.Failure(SalesOrderErrors.InvalidDeliveryReversal(lineNumber));
        }

        line.AddDelivered(-birds, -weightKg);

        if (Status != SalesOrderStatus.Closed)
        {
            UpdateDeliveryStatus();
        }

        return Result.Success();
    }

    private void UpdateDeliveryStatus()
    {
        if (_lines.TrueForAll(l => l.DeliveredBirds == 0))
        {
            Status = SalesOrderStatus.Approved;
            return;
        }

        Status = _lines.TrueForAll(l => l.OutstandingBirds == 0)
            ? SalesOrderStatus.Delivered
            : SalesOrderStatus.PartiallyDelivered;
    }

    private Result Apply(DateOnly orderDate, DateOnly? deliveryDate, string? notes, IReadOnlyList<SalesOrderLineInput> lines)
    {
        if (lines.Count == 0)
        {
            return Result.Failure(SalesOrderErrors.NoLines);
        }

        if (lines.GroupBy(l => l.ItemId).Any(g => g.Count() > 1))
        {
            return Result.Failure(SalesOrderErrors.DuplicateItem);
        }

        if (lines.Any(l => l.Birds <= 0 || l.EstimatedWeightKg <= 0 || l.PricePerKg.IsNegative || l.PricePerKg.IsZero))
        {
            return Result.Failure(SalesOrderErrors.InvalidLine);
        }

        if (deliveryDate is not null && deliveryDate < orderDate)
        {
            return Result.Failure(SalesOrderErrors.DeliveryBeforeOrder);
        }

        OrderDate = orderDate;
        DeliveryDate = deliveryDate;
        Notes = notes;

        _lines.Clear();
        _lines.AddRange(lines.Select((l, index) => new SalesOrderLine(
            Id, index + 1, l.ItemId, l.Birds, l.EstimatedWeightKg, l.PricePerKg, l.TaxCodeId)));

        return Result.Success();
    }
}

/// <param name="PricePerKg">Price per kg live weight, excluding VAT.</param>
/// <param name="TaxCodeId">VAT code applied on the invoice (e.g. PPN dibebaskan for live birds).</param>
public sealed record SalesOrderLineInput(Guid ItemId, int Birds, decimal EstimatedWeightKg, Money PricePerKg, Guid? TaxCodeId);

/// <param name="Limit">The customer's credit limit; zero means no credit.</param>
/// <param name="Exposure">What the customer already owes or has on order, excluding the order being approved.</param>
public sealed record CreditCheck(Money Limit, Money Exposure);

public enum SalesOrderStatus
{
    Draft = 1,
    Approved = 2,
    PartiallyDelivered = 3,
    Delivered = 4,

    /// <summary>
    /// Partially delivered and closed; the rest will not be delivered.
    /// </summary>
    Closed = 5,

    Cancelled = 9
}
