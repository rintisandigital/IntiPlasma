using SharedKernel;

namespace Domain.Inventory.Stock;

/// <summary>
/// On-hand quantity and value of one item in one warehouse, valued at <b>moving average</b> cost:
/// a receipt adds its cost to the pool, an issue removes quantity at the current average cost.
/// Every change produces a <see cref="StockLedgerEntry"/> (kartu stok). Stock can never go negative.
/// Concurrent updates of the same balance are serialized by the aggregate's concurrency token.
/// </summary>
public sealed class StockBalance : AggregateRoot
{
    private StockBalance(Guid id, Guid warehouseId, Guid itemId)
        : base(id)
    {
        WarehouseId = warehouseId;
        ItemId = itemId;
        Value = Money.Zero;
    }

    private StockBalance()
    {
    }

    public Guid WarehouseId { get; private set; }
    public Guid ItemId { get; private set; }

    /// <summary>
    /// Quantity in the item's base unit.
    /// </summary>
    public decimal Quantity { get; private set; }

    public Money Value { get; private set; }

    public decimal AverageCost => Quantity == 0 ? 0 : decimal.Round(Value.Amount / Quantity, StockLedgerEntry.UnitCostDecimals);

    public static StockBalance Open(Guid warehouseId, Guid itemId) =>
        new(Guid.CreateVersion7(), warehouseId, itemId);

    /// <summary>
    /// Adds stock with its total value. Taking the value (not a unit cost) keeps transfers exact: the value that
    /// leaves the source warehouse is the value that arrives at the destination, without rounding differences.
    /// </summary>
    public Result<StockLedgerEntry> Receive(StockMovement movement, decimal quantity, Money value)
    {
        if (quantity <= 0 || value.IsNegative)
        {
            return Result.Failure<StockLedgerEntry>(StockErrors.InvalidQuantity);
        }

        decimal unitCost = decimal.Round(value.Amount / quantity, StockLedgerEntry.UnitCostDecimals);

        Quantity += quantity;
        Value += value;

        return CreateEntry(movement, quantity, unitCost, value);
    }

    /// <summary>
    /// Removes stock at the current average cost. Issuing the whole quantity also removes the whole value,
    /// so no rounding residue is left behind.
    /// </summary>
    public Result<StockLedgerEntry> Issue(StockMovement movement, decimal quantity)
    {
        if (quantity <= 0)
        {
            return Result.Failure<StockLedgerEntry>(StockErrors.InvalidQuantity);
        }

        if (quantity > Quantity)
        {
            return Result.Failure<StockLedgerEntry>(StockErrors.InsufficientStock(ItemId, WarehouseId, Quantity, quantity));
        }

        decimal unitCost = AverageCost;
        Money value = quantity == Quantity ? Value : new Money(quantity * unitCost);

        Quantity -= quantity;
        Value -= value;

        return CreateEntry(movement, -quantity, unitCost, -value);
    }

    private StockLedgerEntry CreateEntry(StockMovement movement, decimal quantity, decimal unitCost, Money value) =>
        new(Guid.CreateVersion7(), WarehouseId, ItemId, movement, quantity, unitCost, value, Quantity, Value);
}
