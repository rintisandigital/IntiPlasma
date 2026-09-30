using SharedKernel;

namespace Domain.Inventory.StockTransfers;

public sealed class StockTransferLine
{
    internal StockTransferLine(
        Guid stockTransferId,
        int lineNumber,
        Guid itemId,
        Guid uomId,
        decimal quantity,
        decimal baseQuantity)
    {
        StockTransferId = stockTransferId;
        LineNumber = lineNumber;
        ItemId = itemId;
        UomId = uomId;
        Quantity = quantity;
        BaseQuantity = baseQuantity;
        Value = Money.Zero;
    }

    private StockTransferLine()
    {
    }

    public Guid StockTransferId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid UomId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal BaseQuantity { get; private set; }

    /// <summary>
    /// Moving average cost per base unit at the moment of transfer.
    /// </summary>
    public decimal UnitCost { get; private set; }

    public Money Value { get; private set; }

    internal void SetCost(decimal unitCost, Money value)
    {
        UnitCost = unitCost;
        Value = value;
    }
}
