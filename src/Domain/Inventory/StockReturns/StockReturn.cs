using Domain.Common;
using Domain.MasterData.Warehouses;
using SharedKernel;

namespace Domain.Inventory.StockReturns;

/// <summary>
/// Retur sapronak: leftover feed or OVK sent back from a coop warehouse to a central warehouse of the same branch.
/// It takes the value out of the cycle again (Dr persediaan / Cr ayam dalam proses). Moving feed from one coop to
/// another always goes through a central warehouse: a return from the first coop followed by a transfer to the second.
/// </summary>
public sealed class StockReturn : AggregateRoot, IHasDocuments
{
    private readonly List<StockReturnLine> _lines = [];

    private StockReturn(Guid id)
        : base(id)
    {
    }

    private StockReturn()
    {
    }

    public string Number { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid FromWarehouseId { get; private set; }
    public Guid ToWarehouseId { get; private set; }

    /// <summary>
    /// The cycle the stock is taken out of.
    /// </summary>
    public Guid CycleId { get; private set; }

    public DateOnly ReturnDate { get; private set; }
    public string Reason { get; private set; }
    public string? Notes { get; private set; }
    public IReadOnlyCollection<StockReturnLine> Lines => [.. _lines];

    /// <summary>
    /// Lampiran: ids of the attached photos and documents.
    /// </summary>
    public Guid[] Documents { get; private set; } = [];

    public Result SetDocuments(IEnumerable<Guid>? documents) =>
        DocumentList.Apply(documents, value => Documents = value);

    public static Result<StockReturn> Create(
        string number,
        Warehouse from,
        Warehouse to,
        Guid cycleId,
        DateOnly returnDate,
        string reason,
        string? notes,
        IReadOnlyList<StockReturnLineInput> lines)
    {
        if (from.Type != WarehouseType.Coop)
        {
            return Result.Failure<StockReturn>(StockReturnErrors.SourceMustBeCoop);
        }

        if (to.Type != WarehouseType.Central)
        {
            return Result.Failure<StockReturn>(StockReturnErrors.DestinationMustBeCentral);
        }

        if (from.BranchId != to.BranchId)
        {
            return Result.Failure<StockReturn>(StockTransfers.StockTransferErrors.CrossBranch);
        }

        if (!to.IsActive)
        {
            return Result.Failure<StockReturn>(StockTransfers.StockTransferErrors.InactiveWarehouse);
        }

        if (lines.Count == 0 || lines.Any(l => l.Quantity <= 0 || l.BaseQuantity <= 0) ||
            lines.GroupBy(l => l.ItemId).Any(g => g.Count() > 1))
        {
            return Result.Failure<StockReturn>(StockTransfers.StockTransferErrors.InvalidLines);
        }

        var stockReturn = new StockReturn(Guid.CreateVersion7())
        {
            Number = number,
            BranchId = from.BranchId,
            FromWarehouseId = from.Id,
            ToWarehouseId = to.Id,
            CycleId = cycleId,
            ReturnDate = returnDate,
            Reason = reason,
            Notes = notes
        };

        stockReturn._lines.AddRange(lines.Select((l, index) => new StockReturnLine(
            stockReturn.Id, index + 1, l.ItemId, l.UomId, l.Quantity, l.BaseQuantity)));

        stockReturn.Raise(new StockReturnPostedDomainEvent(stockReturn.Id));

        return stockReturn;
    }

    public void RecordCost(int lineNumber, decimal unitCost, Money value)
    {
        _lines.Single(l => l.LineNumber == lineNumber).SetCost(unitCost, value);
    }
}

public sealed record StockReturnLineInput(Guid ItemId, Guid UomId, decimal Quantity, decimal BaseQuantity);
