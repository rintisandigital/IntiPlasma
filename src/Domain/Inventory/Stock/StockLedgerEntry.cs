using SharedKernel;

namespace Domain.Inventory.Stock;

/// <summary>
/// One immutable line of the stock card (kartu stok): a signed movement and the balance after it.
/// </summary>
public sealed class StockLedgerEntry : Entity
{
    public const int UnitCostDecimals = 6;

    internal StockLedgerEntry(
        Guid id,
        Guid warehouseId,
        Guid itemId,
        StockMovement movement,
        decimal quantity,
        decimal unitCost,
        Money value,
        decimal balanceQuantity,
        Money balanceValue)
        : base(id)
    {
        WarehouseId = warehouseId;
        ItemId = itemId;
        Date = movement.Date;
        Type = movement.Type;
        SourceType = movement.SourceType;
        SourceId = movement.SourceId;
        SourceNumber = movement.SourceNumber;
        CycleId = movement.CycleId;
        Quantity = quantity;
        UnitCost = unitCost;
        Value = value;
        BalanceQuantity = balanceQuantity;
        BalanceValue = balanceValue;
    }

    private StockLedgerEntry()
    {
    }

    public Guid WarehouseId { get; private set; }
    public Guid ItemId { get; private set; }
    public DateOnly Date { get; private set; }
    public StockMovementType Type { get; private set; }
    public string SourceType { get; private set; }
    public Guid SourceId { get; private set; }
    public string SourceNumber { get; private set; }

    /// <summary>
    /// The production cycle the stock was received for or transferred to (coop warehouses).
    /// </summary>
    public Guid? CycleId { get; private set; }

    /// <summary>
    /// Base-unit quantity; positive for stock in, negative for stock out.
    /// </summary>
    public decimal Quantity { get; private set; }

    public decimal UnitCost { get; private set; }

    /// <summary>
    /// Signed value of the movement.
    /// </summary>
    public Money Value { get; private set; }

    public decimal BalanceQuantity { get; private set; }
    public Money BalanceValue { get; private set; }
}

/// <summary>
/// Why and from which document stock moved.
/// </summary>
public sealed record StockMovement(
    DateOnly Date,
    StockMovementType Type,
    string SourceType,
    Guid SourceId,
    string SourceNumber,
    Guid? CycleId);

public enum StockMovementType
{
    /// <summary>
    /// Penerimaan barang dari vendor.
    /// </summary>
    Receipt = 1,

    /// <summary>
    /// Keluar karena transfer ke gudang lain.
    /// </summary>
    TransferOut = 2,

    /// <summary>
    /// Masuk dari transfer gudang lain.
    /// </summary>
    TransferIn = 3,

    /// <summary>
    /// DOC ditebar ke kandang (chick-in).
    /// </summary>
    ChickIn = 4,

    /// <summary>
    /// Pemakaian pakan/OVK harian (recording).
    /// </summary>
    Usage = 5,

    /// <summary>
    /// Pembatalan pemakaian karena revisi recording.
    /// </summary>
    UsageReversal = 6,

    /// <summary>
    /// Keluar dari gudang kandang karena retur.
    /// </summary>
    ReturnOut = 7,

    /// <summary>
    /// Masuk ke gudang induk dari retur kandang.
    /// </summary>
    ReturnIn = 8
}
