using SharedKernel;

namespace Domain.Procurement.PurchaseOrders;

/// <summary>
/// Purchase order for sapronak (DOC, pakan, OVK) to one vendor. Draft → Approved → (Partially) Received → Closed.
/// Receipts are recorded against the order lines; an order cannot be over-received.
/// </summary>
public sealed class PurchaseOrder : AggregateRoot
{
    private readonly List<PurchaseOrderLine> _lines = [];

    private PurchaseOrder(Guid id, string number, Guid branchId, Guid vendorId)
        : base(id)
    {
        Number = number;
        BranchId = branchId;
        VendorId = vendorId;
        Status = PurchaseOrderStatus.Draft;
    }

    private PurchaseOrder()
    {
    }

    public string Number { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid VendorId { get; private set; }
    public DateOnly OrderDate { get; private set; }
    public DateOnly? ExpectedDate { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public IReadOnlyCollection<PurchaseOrderLine> Lines => [.. _lines];

    public Money Subtotal => _lines.Aggregate(Money.Zero, (total, line) => total + line.Amount);

    public bool CanReceive => Status is PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived;

    public static Result<PurchaseOrder> Create(
        string number,
        Guid branchId,
        Guid vendorId,
        DateOnly orderDate,
        DateOnly? expectedDate,
        string? notes,
        IReadOnlyList<PurchaseOrderLineInput> lines)
    {
        var order = new PurchaseOrder(Guid.CreateVersion7(), number, branchId, vendorId);

        Result result = order.Apply(orderDate, expectedDate, notes, lines);

        return result.IsSuccess ? order : Result.Failure<PurchaseOrder>(result.Error);
    }

    public Result Update(DateOnly orderDate, DateOnly? expectedDate, string? notes, IReadOnlyList<PurchaseOrderLineInput> lines)
    {
        if (Status != PurchaseOrderStatus.Draft)
        {
            return Result.Failure(PurchaseOrderErrors.NotDraft(Id));
        }

        return Apply(orderDate, expectedDate, notes, lines);
    }

    public Result Approve(Guid approverId, DateTime utcNow)
    {
        if (Status != PurchaseOrderStatus.Draft)
        {
            return Result.Failure(PurchaseOrderErrors.InvalidTransition(Status, PurchaseOrderStatus.Approved));
        }

        Status = PurchaseOrderStatus.Approved;
        ApprovedBy = approverId;
        ApprovedAtUtc = utcNow;

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Approved))
        {
            return Result.Failure(PurchaseOrderErrors.InvalidTransition(Status, PurchaseOrderStatus.Cancelled));
        }

        Status = PurchaseOrderStatus.Cancelled;
        CancellationReason = reason;

        return Result.Success();
    }

    /// <summary>
    /// Closes a partially received order: the outstanding quantity will not be delivered.
    /// </summary>
    public Result Close()
    {
        if (Status != PurchaseOrderStatus.PartiallyReceived)
        {
            return Result.Failure(PurchaseOrderErrors.InvalidTransition(Status, PurchaseOrderStatus.Closed));
        }

        Status = PurchaseOrderStatus.Closed;

        return Result.Success();
    }

    /// <summary>
    /// Records a delivered quantity (in the order line's unit) against a line.
    /// </summary>
    public Result RegisterReceipt(int lineNumber, decimal quantity)
    {
        if (!CanReceive)
        {
            return Result.Failure(PurchaseOrderErrors.NotReceivable(Id));
        }

        PurchaseOrderLine? line = _lines.Find(l => l.LineNumber == lineNumber);
        if (line is null)
        {
            return Result.Failure(PurchaseOrderErrors.LineNotFound(lineNumber));
        }

        if (quantity <= 0 || quantity > line.OutstandingQuantity)
        {
            return Result.Failure(PurchaseOrderErrors.OverReceipt(lineNumber, line.OutstandingQuantity));
        }

        line.AddReceived(quantity);

        Status = _lines.TrueForAll(l => l.OutstandingQuantity == 0)
            ? PurchaseOrderStatus.Received
            : PurchaseOrderStatus.PartiallyReceived;

        return Result.Success();
    }

    private Result Apply(DateOnly orderDate, DateOnly? expectedDate, string? notes, IReadOnlyList<PurchaseOrderLineInput> lines)
    {
        if (lines.Count == 0)
        {
            return Result.Failure(PurchaseOrderErrors.NoLines);
        }

        if (lines.GroupBy(l => (l.ItemId, l.UomId)).Any(g => g.Count() > 1))
        {
            return Result.Failure(PurchaseOrderErrors.DuplicateItem);
        }

        if (lines.Any(l => l.Quantity <= 0 || l.UnitPrice.IsNegative || l.UnitPrice.IsZero))
        {
            return Result.Failure(PurchaseOrderErrors.InvalidLine);
        }

        if (expectedDate is not null && expectedDate < orderDate)
        {
            return Result.Failure(PurchaseOrderErrors.ExpectedBeforeOrder);
        }

        OrderDate = orderDate;
        ExpectedDate = expectedDate;
        Notes = notes;

        _lines.Clear();
        _lines.AddRange(lines.Select((l, index) => new PurchaseOrderLine(
            Id, index + 1, l.ItemId, l.UomId, l.Quantity, l.UnitPrice, l.TaxCodeId)));

        return Result.Success();
    }
}

public sealed record PurchaseOrderLineInput(Guid ItemId, Guid UomId, decimal Quantity, Money UnitPrice, Guid? TaxCodeId);

public enum PurchaseOrderStatus
{
    Draft = 1,
    Approved = 2,
    PartiallyReceived = 3,
    Received = 4,

    /// <summary>
    /// Partially received and closed; the rest will not be delivered.
    /// </summary>
    Closed = 5,

    Cancelled = 9
}
