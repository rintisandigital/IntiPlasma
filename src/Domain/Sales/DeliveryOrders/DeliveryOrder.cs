using Domain.Sales.SalesOrders;
using SharedKernel;

namespace Domain.Sales.DeliveryOrders;

/// <summary>
/// Delivery order (DO / surat jalan) of harvested birds against a sales order. Every line is one harvest record of a
/// cycle (panen, typically one truck), delivered whole: the weighed birds and kg of the harvest are what is sold.
/// A harvest can only be on one delivery order that is not cancelled (also enforced by a unique index).
/// Delivered → Invoiced (on a sales invoice); only an uninvoiced delivery can be cancelled.
/// </summary>
public sealed class DeliveryOrder : AggregateRoot
{
    private readonly List<DeliveryOrderLine> _lines = [];

    private DeliveryOrder(Guid id)
        : base(id)
    {
    }

    private DeliveryOrder()
    {
    }

    public string Number { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid SalesOrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public DateOnly DeliveryDate { get; private set; }
    public string? VehicleNumber { get; private set; }
    public string? DriverName { get; private set; }
    public string? Notes { get; private set; }
    public DeliveryOrderStatus Status { get; private set; }

    /// <summary>
    /// The sales invoice (draft or posted) the delivery is billed on.
    /// </summary>
    public Guid? SalesInvoiceId { get; private set; }

    public string? CancellationReason { get; private set; }
    public IReadOnlyCollection<DeliveryOrderLine> Lines => [.. _lines];

    public Money Amount => _lines.Aggregate(Money.Zero, (total, line) => total + line.Amount);

    /// <summary>
    /// Creates the delivery order. Does not change the sales order: the caller registers the delivered birds per
    /// order line afterwards (so it can validate first with a placeholder number).
    /// </summary>
    public static Result<DeliveryOrder> Create(
        string number,
        SalesOrder order,
        DateOnly deliveryDate,
        string? vehicleNumber,
        string? driverName,
        string? notes,
        IReadOnlyList<DeliveryOrderLineInput> lines)
    {
        Result validation = Validate(order, deliveryDate, lines);
        if (validation.IsFailure)
        {
            return Result.Failure<DeliveryOrder>(validation.Error);
        }

        var deliveryOrder = new DeliveryOrder(Guid.CreateVersion7())
        {
            Number = number,
            BranchId = order.BranchId,
            SalesOrderId = order.Id,
            CustomerId = order.CustomerId,
            DeliveryDate = deliveryDate,
            VehicleNumber = vehicleNumber,
            DriverName = driverName,
            Notes = notes,
            Status = DeliveryOrderStatus.Delivered
        };

        foreach ((DeliveryOrderLineInput input, int index) in lines.Select((l, i) => (l, i)))
        {
            SalesOrderLine orderLine = order.Lines.Single(l => l.LineNumber == input.SalesOrderLineNumber);
            HarvestToDeliver harvest = input.Harvest;

            deliveryOrder._lines.Add(new DeliveryOrderLine(
                deliveryOrder.Id,
                index + 1,
                orderLine.LineNumber,
                orderLine.ItemId,
                harvest.HarvestId,
                harvest.CycleId,
                harvest.Birds,
                harvest.WeightKg,
                orderLine.PricePerKg,
                orderLine.TaxCodeId));
        }

        return deliveryOrder;
    }

    /// <summary>
    /// Cancels an uninvoiced delivery; the caller takes the delivered birds back on the sales order.
    /// The harvests become available for another delivery order.
    /// </summary>
    public Result Cancel(string reason)
    {
        if (Status != DeliveryOrderStatus.Delivered)
        {
            return Result.Failure(DeliveryOrderErrors.InvalidTransition(Status, DeliveryOrderStatus.Cancelled));
        }

        Status = DeliveryOrderStatus.Cancelled;
        CancellationReason = reason;

        foreach (DeliveryOrderLine line in _lines)
        {
            line.MarkCancelled();
        }

        return Result.Success();
    }

    internal Result MarkInvoiced(Guid salesInvoiceId)
    {
        if (Status != DeliveryOrderStatus.Delivered)
        {
            return Result.Failure(DeliveryOrderErrors.NotInvoiceable(Id));
        }

        Status = DeliveryOrderStatus.Invoiced;
        SalesInvoiceId = salesInvoiceId;

        return Result.Success();
    }

    /// <summary>
    /// Makes the delivery billable again after its draft invoice was cancelled.
    /// </summary>
    internal void ReleaseInvoice()
    {
        Status = DeliveryOrderStatus.Delivered;
        SalesInvoiceId = null;
    }

    private static Result Validate(SalesOrder order, DateOnly deliveryDate, IReadOnlyList<DeliveryOrderLineInput> lines)
    {
        if (!order.CanDeliver)
        {
            return Result.Failure(SalesOrderErrors.NotDeliverable(order.Id));
        }

        if (deliveryDate < order.OrderDate)
        {
            return Result.Failure(DeliveryOrderErrors.BeforeOrderDate);
        }

        if (lines.Count == 0)
        {
            return Result.Failure(DeliveryOrderErrors.NoLines);
        }

        if (lines.GroupBy(l => l.Harvest.HarvestId).Any(g => g.Count() > 1))
        {
            return Result.Failure(DeliveryOrderErrors.DuplicateHarvest);
        }

        foreach (DeliveryOrderLineInput line in lines)
        {
            if (line.Harvest.BranchId != order.BranchId)
            {
                return Result.Failure(DeliveryOrderErrors.HarvestBranchMismatch);
            }

            if (deliveryDate < line.Harvest.Date)
            {
                return Result.Failure(DeliveryOrderErrors.BeforeHarvestDate(line.Harvest.Date));
            }
        }

        foreach (IGrouping<int, DeliveryOrderLineInput> group in lines.GroupBy(l => l.SalesOrderLineNumber))
        {
            SalesOrderLine? orderLine = order.Lines.FirstOrDefault(l => l.LineNumber == group.Key);
            if (orderLine is null)
            {
                return Result.Failure(SalesOrderErrors.LineNotFound(group.Key));
            }

            if (group.Sum(l => l.Harvest.Birds) > orderLine.OutstandingBirds)
            {
                return Result.Failure(SalesOrderErrors.OverDelivery(orderLine.LineNumber, orderLine.OutstandingBirds));
            }
        }

        return Result.Success();
    }
}

public sealed record DeliveryOrderLineInput(int SalesOrderLineNumber, HarvestToDeliver Harvest);

/// <summary>
/// A harvest record of a cycle (partnership context), with the branch of its cycle.
/// </summary>
public sealed record HarvestToDeliver(Guid HarvestId, Guid CycleId, Guid BranchId, DateOnly Date, int Birds, decimal WeightKg);

public enum DeliveryOrderStatus
{
    Delivered = 1,
    Invoiced = 2,
    Cancelled = 9
}
