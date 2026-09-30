using SharedKernel;

namespace Domain.Procurement.PurchaseOrders;

public sealed class PurchaseOrderLine
{
    internal PurchaseOrderLine(
        Guid purchaseOrderId,
        int lineNumber,
        Guid itemId,
        Guid uomId,
        decimal quantity,
        Money unitPrice,
        Guid? taxCodeId)
    {
        PurchaseOrderId = purchaseOrderId;
        LineNumber = lineNumber;
        ItemId = itemId;
        UomId = uomId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxCodeId = taxCodeId;
    }

    private PurchaseOrderLine()
    {
    }

    public Guid PurchaseOrderId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid ItemId { get; private set; }

    /// <summary>
    /// Ordering unit (the item's base unit or one of its conversions, e.g. SAK).
    /// </summary>
    public Guid UomId { get; private set; }

    public decimal Quantity { get; private set; }

    /// <summary>
    /// Price per ordering unit, excluding VAT.
    /// </summary>
    public Money UnitPrice { get; private set; }

    /// <summary>
    /// VAT code applied when the vendor invoice is registered (phase 6).
    /// </summary>
    public Guid? TaxCodeId { get; private set; }

    public decimal QuantityReceived { get; private set; }

    public decimal OutstandingQuantity => Quantity - QuantityReceived;

    public Money Amount => UnitPrice * Quantity;

    internal void AddReceived(decimal quantity)
    {
        QuantityReceived += quantity;
    }
}
