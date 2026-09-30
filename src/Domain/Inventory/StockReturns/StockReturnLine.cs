using SharedKernel;

namespace Domain.Inventory.StockReturns;

public sealed class StockReturnLine
{
    internal StockReturnLine(Guid stockReturnId, int lineNumber, Guid itemId, Guid uomId, decimal quantity, decimal baseQuantity)
    {
        StockReturnId = stockReturnId;
        LineNumber = lineNumber;
        ItemId = itemId;
        UomId = uomId;
        Quantity = quantity;
        BaseQuantity = baseQuantity;
        Value = Money.Zero;
    }

    private StockReturnLine()
    {
    }

    public Guid StockReturnId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid UomId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal BaseQuantity { get; private set; }

    /// <summary>
    /// Moving average cost per base unit in the coop warehouse at the moment of return.
    /// </summary>
    public decimal UnitCost { get; private set; }

    public Money Value { get; private set; }

    internal void SetCost(decimal unitCost, Money value)
    {
        UnitCost = unitCost;
        Value = value;
    }
}
