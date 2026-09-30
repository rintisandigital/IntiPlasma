using Domain.MasterData.Warehouses;
using SharedKernel;

namespace Domain.Inventory.StockTransfers;

/// <summary>
/// Transfer (surat jalan) of sapronak out of a central warehouse, either to another central warehouse of the same
/// branch or to a coop warehouse. A transfer to a coop is charged to the coop's open production cycle: from then on
/// the stock is part of the cycle cost (persediaan ayam dalam proses). Lines are valued at moving average cost when
/// the stock is issued.
/// </summary>
public sealed class StockTransfer : AggregateRoot
{
    private readonly List<StockTransferLine> _lines = [];

    private StockTransfer(Guid id)
        : base(id)
    {
    }

    private StockTransfer()
    {
    }

    public string Number { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid FromWarehouseId { get; private set; }
    public Guid ToWarehouseId { get; private set; }

    /// <summary>
    /// Set for transfers to a coop warehouse.
    /// </summary>
    public Guid? CycleId { get; private set; }

    public DateOnly TransferDate { get; private set; }
    public string? Notes { get; private set; }
    public IReadOnlyCollection<StockTransferLine> Lines => [.. _lines];

    public bool IsToCycle => CycleId is not null;

    public Money TotalValue => _lines.Aggregate(Money.Zero, (total, line) => total + line.Value);

    public static Result<StockTransfer> Create(
        string number,
        Warehouse from,
        Warehouse to,
        Guid? coopCycleId,
        DateOnly transferDate,
        string? notes,
        IReadOnlyList<StockTransferLineInput> lines)
    {
        if (from.Id == to.Id)
        {
            return Result.Failure<StockTransfer>(StockTransferErrors.SameWarehouse);
        }

        if (from.Type != WarehouseType.Central)
        {
            return Result.Failure<StockTransfer>(StockTransferErrors.SourceMustBeCentral);
        }

        if (from.BranchId != to.BranchId)
        {
            return Result.Failure<StockTransfer>(StockTransferErrors.CrossBranch);
        }

        if (!from.IsActive || !to.IsActive)
        {
            return Result.Failure<StockTransfer>(StockTransferErrors.InactiveWarehouse);
        }

        if (to.Type == WarehouseType.Coop && coopCycleId is null)
        {
            return Result.Failure<StockTransfer>(GoodsReceipts.GoodsReceiptErrors.CoopHasNoOpenCycle(to.CoopId!.Value));
        }

        if (lines.Count == 0 || lines.Any(l => l.Quantity <= 0 || l.BaseQuantity <= 0) ||
            lines.GroupBy(l => l.ItemId).Any(g => g.Count() > 1))
        {
            return Result.Failure<StockTransfer>(StockTransferErrors.InvalidLines);
        }

        var transfer = new StockTransfer(Guid.CreateVersion7())
        {
            Number = number,
            BranchId = from.BranchId,
            FromWarehouseId = from.Id,
            ToWarehouseId = to.Id,
            CycleId = to.Type == WarehouseType.Coop ? coopCycleId : null,
            TransferDate = transferDate,
            Notes = notes
        };

        transfer._lines.AddRange(lines.Select((l, index) => new StockTransferLine(
            transfer.Id, index + 1, l.ItemId, l.UomId, l.Quantity, l.BaseQuantity)));

        transfer.Raise(new StockTransferPostedDomainEvent(transfer.Id));

        return transfer;
    }

    /// <summary>
    /// Records the moving average cost at which each line left the source warehouse.
    /// </summary>
    public void RecordCost(int lineNumber, decimal unitCost, Money value)
    {
        _lines.Single(l => l.LineNumber == lineNumber).SetCost(unitCost, value);
    }
}

/// <param name="Quantity">Quantity in <paramref name="UomId"/>.</param>
/// <param name="BaseQuantity">The same quantity in the item's base unit.</param>
public sealed record StockTransferLineInput(Guid ItemId, Guid UomId, decimal Quantity, decimal BaseQuantity);
