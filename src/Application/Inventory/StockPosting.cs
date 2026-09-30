using Application.Abstractions.Data;
using Domain.Inventory.Stock;
using Domain.Inventory.StockReturns;
using Domain.Inventory.StockTransfers;
using Domain.MasterData.Items;
using SharedKernel;

namespace Application.Inventory;

/// <summary>
/// Moves stock for transfers and returns: out of the source at moving average cost and into the destination
/// with exactly that value. Several documents in one command share the same <see cref="StockBalances"/>, so a
/// feed mutation (coop → central → coop) sees the central stock the return just added.
/// </summary>
internal static class StockPosting
{
    public static async Task<Result> PostTransferAsync(
        IApplicationDbContext context,
        StockBalances balances,
        StockTransfer transfer,
        CancellationToken cancellationToken)
    {
        var outMovement = new StockMovement(
            transfer.TransferDate, StockMovementType.TransferOut, nameof(StockTransfer), transfer.Id, transfer.Number, transfer.CycleId);
        StockMovement inMovement = outMovement with { Type = StockMovementType.TransferIn };

        foreach (StockTransferLine line in transfer.Lines)
        {
            Result<Money> moved = await MoveAsync(
                context, balances, transfer.FromWarehouseId, transfer.ToWarehouseId, line.ItemId, line.BaseQuantity,
                outMovement, inMovement, cancellationToken);

            if (moved.IsFailure)
            {
                return moved;
            }

            transfer.RecordCost(line.LineNumber, UnitCost(moved.Value, line.BaseQuantity), moved.Value);
        }

        return Result.Success();
    }

    public static async Task<Result> PostReturnAsync(
        IApplicationDbContext context,
        StockBalances balances,
        StockReturn stockReturn,
        CancellationToken cancellationToken)
    {
        var outMovement = new StockMovement(
            stockReturn.ReturnDate, StockMovementType.ReturnOut, nameof(StockReturn), stockReturn.Id, stockReturn.Number, stockReturn.CycleId);
        StockMovement inMovement = outMovement with { Type = StockMovementType.ReturnIn };

        foreach (StockReturnLine line in stockReturn.Lines)
        {
            Result<Money> moved = await MoveAsync(
                context, balances, stockReturn.FromWarehouseId, stockReturn.ToWarehouseId, line.ItemId, line.BaseQuantity,
                outMovement, inMovement, cancellationToken);

            if (moved.IsFailure)
            {
                return moved;
            }

            stockReturn.RecordCost(line.LineNumber, UnitCost(moved.Value, line.BaseQuantity), moved.Value);
        }

        return Result.Success();
    }

    /// <summary>
    /// Converts requested quantities to base units and checks the item categories allowed for the document.
    /// </summary>
    public static Result<List<BaseQuantityLine>> ToBaseQuantities(
        Dictionary<Guid, Item> items,
        IEnumerable<(Guid ItemId, Guid UomId, decimal Quantity)> requests,
        ItemCategory[]? allowedCategories,
        Error categoryError)
    {
        var lines = new List<BaseQuantityLine>();

        foreach ((Guid itemId, Guid uomId, decimal quantity) in requests)
        {
            if (!items.TryGetValue(itemId, out Item? item))
            {
                return Result.Failure<List<BaseQuantityLine>>(ItemErrors.NotFound(itemId));
            }

            if (allowedCategories is not null && !allowedCategories.Contains(item.Category))
            {
                return Result.Failure<List<BaseQuantityLine>>(categoryError);
            }

            Result<decimal> baseQuantity = item.ConvertToBase(uomId, quantity);
            if (baseQuantity.IsFailure)
            {
                return Result.Failure<List<BaseQuantityLine>>(baseQuantity.Error);
            }

            lines.Add(new BaseQuantityLine(itemId, uomId, quantity, baseQuantity.Value));
        }

        return lines;
    }

    private static async Task<Result<Money>> MoveAsync(
        IApplicationDbContext context,
        StockBalances balances,
        Guid fromWarehouseId,
        Guid toWarehouseId,
        Guid itemId,
        decimal quantity,
        StockMovement outMovement,
        StockMovement inMovement,
        CancellationToken cancellationToken)
    {
        StockBalance source = await balances.GetAsync(fromWarehouseId, itemId, cancellationToken);

        Result<StockLedgerEntry> issued = source.Issue(outMovement, quantity);
        if (issued.IsFailure)
        {
            return Result.Failure<Money>(issued.Error);
        }

        Money value = -issued.Value.Value;

        StockBalance destination = await balances.GetAsync(toWarehouseId, itemId, cancellationToken);

        Result<StockLedgerEntry> received = destination.Receive(inMovement, quantity, value);
        if (received.IsFailure)
        {
            return Result.Failure<Money>(received.Error);
        }

        context.StockLedgerEntries.AddRange(issued.Value, received.Value);

        return value;
    }

    private static decimal UnitCost(Money value, decimal quantity) =>
        decimal.Round(value.Amount / quantity, StockLedgerEntry.UnitCostDecimals);
}

/// <summary>
/// A requested quantity together with the same quantity in the item's base unit.
/// </summary>
internal sealed record BaseQuantityLine(Guid ItemId, Guid UomId, decimal Quantity, decimal BaseQuantity);
