using SharedKernel;

namespace Domain.Sales.DeliveryOrders;

public sealed class DeliveryOrderLine
{
    internal DeliveryOrderLine(
        Guid deliveryOrderId,
        int lineNumber,
        int salesOrderLineNumber,
        Guid itemId,
        Guid harvestId,
        Guid cycleId,
        int birds,
        decimal weightKg,
        Money pricePerKg,
        Guid? taxCodeId)
    {
        DeliveryOrderId = deliveryOrderId;
        LineNumber = lineNumber;
        SalesOrderLineNumber = salesOrderLineNumber;
        ItemId = itemId;
        HarvestId = harvestId;
        CycleId = cycleId;
        Birds = birds;
        WeightKg = weightKg;
        // Own copy of the order line's price: a value object instance is never shared between two entities.
        PricePerKg = pricePerKg with { };
        TaxCodeId = taxCodeId;
        Amount = pricePerKg * weightKg;
    }

    private DeliveryOrderLine()
    {
    }

    public Guid DeliveryOrderId { get; private set; }
    public int LineNumber { get; private set; }
    public int SalesOrderLineNumber { get; private set; }
    public Guid ItemId { get; private set; }

    /// <summary>
    /// The harvest record (cycle_harvests) delivered on this line.
    /// </summary>
    public Guid HarvestId { get; private set; }

    public Guid CycleId { get; private set; }
    public int Birds { get; private set; }
    public decimal WeightKg { get; private set; }

    /// <summary>
    /// Price per kg taken from the sales order line, excluding VAT.
    /// </summary>
    public Money PricePerKg { get; private set; }

    public Guid? TaxCodeId { get; private set; }

    /// <summary>
    /// Weighed kg × price per kg, excluding VAT.
    /// </summary>
    public Money Amount { get; private set; }

    /// <summary>
    /// Copy of the delivery order's cancellation, so the database can keep a harvest on one active delivery only.
    /// </summary>
    public bool IsCancelled { get; private set; }

    internal void MarkCancelled()
    {
        IsCancelled = true;
    }
}
