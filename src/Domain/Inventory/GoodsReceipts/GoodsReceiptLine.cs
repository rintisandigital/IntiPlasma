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
}
