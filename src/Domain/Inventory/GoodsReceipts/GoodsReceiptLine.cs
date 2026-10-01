using SharedKernel;

namespace Domain.Inventory.GoodsReceipts;

public sealed class GoodsReceiptLine
{
    internal GoodsReceiptLine(
        Guid goodsReceiptId,
        int lineNumber,
        int purchaseOrderLineNumber,
        Guid itemId,
        Guid uomId,
        decimal quantity,
        decimal baseQuantity,
        decimal unitCost,
        Money value)
    {
        GoodsReceiptId = goodsReceiptId;
        LineNumber = lineNumber;
        PurchaseOrderLineNumber = purchaseOrderLineNumber;
        ItemId = itemId;
        UomId = uomId;
        Quantity = quantity;
        BaseQuantity = baseQuantity;
        UnitCost = unitCost;
        Value = value;
    }

    private GoodsReceiptLine()
    {
    }

    public Guid GoodsReceiptId { get; private set; }
    public int LineNumber { get; private set; }
    public int PurchaseOrderLineNumber { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid UomId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal BaseQuantity { get; private set; }

    /// <summary>
    /// Cost per base unit (order price excluding VAT).
    /// </summary>
    public decimal UnitCost { get; private set; }

    public Money Value { get; private set; }

    /// <summary>
    /// Quantity (in the order line's unit) billed on vendor invoices (draft or posted); the 3-way match
    /// never lets it exceed the received quantity.
    /// </summary>
    public decimal QuantityInvoiced { get; private set; }

    /// <summary>
    /// Receipt value already cleared from GRNI by those invoices.
    /// </summary>
    public Money ValueInvoiced { get; private set; } = new(0m);

    public decimal UninvoicedQuantity => Quantity - QuantityInvoiced;

    /// <summary>
    /// Bills part of the receipt and returns its receipt value; the last part takes the remaining value so the
    /// receipt is cleared from GRNI to the cent.
    /// </summary>
    internal Money Invoice(decimal quantity)
    {
        Money value = quantity == UninvoicedQuantity
            ? Value - ValueInvoiced
            : Value * (quantity / Quantity);

        QuantityInvoiced += quantity;
        ValueInvoiced += value;

        return value;
    }

    internal void ReleaseInvoice(decimal quantity, Money value)
    {
        QuantityInvoiced -= quantity;
        ValueInvoiced -= value;
    }
}
