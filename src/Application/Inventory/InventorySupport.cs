using Application.Abstractions.Data;
using Domain.Inventory.GoodsReceipts;
using Domain.Inventory.Stock;
using Domain.MasterData.Items;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Cycles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Inventory;

internal static class InventorySupport
{
    public static readonly ItemCategory[] SapronakCategories = [ItemCategory.Doc, ItemCategory.Feed, ItemCategory.Ovk];

    /// <summary>
    /// Loads the items (with their unit conversions) referenced by a document.
    /// </summary>
    public static async Task<Dictionary<Guid, Item>> LoadItemsAsync(
        IApplicationDbContext context,
        IEnumerable<Guid> itemIds,
        CancellationToken cancellationToken)
    {
        var ids = itemIds.Distinct().ToList();

        return await context.Items
            .AsNoTracking()
            .Include(i => i.Conversions)
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);
    }

    /// <summary>
    /// For a coop warehouse: the coop's single open (planned, running or harvesting) cycle; null for central warehouses.
    /// </summary>
    public static async Task<Result<Guid?>> ResolveCoopCycleAsync(
        IApplicationDbContext context,
        Warehouse warehouse,
        CancellationToken cancellationToken)
    {
        if (warehouse.Type != WarehouseType.Coop)
        {
            return Result.Success<Guid?>(null);
        }

        Guid? cycleId = await context.ProductionCycles
            .Where(c => c.CoopId == warehouse.CoopId && ProductionCycle.OpenStatuses.Contains(c.Status))
            .Select(c => (Guid?)c.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return cycleId is null
            ? Result.Failure<Guid?>(GoodsReceiptErrors.CoopHasNoOpenCycle(warehouse.CoopId!.Value))
            : cycleId;
    }
}

/// <summary>
/// Loads each (warehouse, item) stock balance once per command and opens it on first use, so several lines
/// touching the same balance update the same tracked aggregate.
/// </summary>
internal sealed class StockBalances(IApplicationDbContext context)
{
    private readonly Dictionary<(Guid WarehouseId, Guid ItemId), StockBalance> _balances = [];

    public async Task<StockBalance> GetAsync(Guid warehouseId, Guid itemId, CancellationToken cancellationToken)
    {
        if (_balances.TryGetValue((warehouseId, itemId), out StockBalance? cached))
        {
            return cached;
        }

        StockBalance? balance = await context.StockBalances
            .SingleOrDefaultAsync(b => b.WarehouseId == warehouseId && b.ItemId == itemId, cancellationToken);

        if (balance is null)
        {
            balance = StockBalance.Open(warehouseId, itemId);
            context.StockBalances.Add(balance);
        }

        _balances[(warehouseId, itemId)] = balance;

        return balance;
    }
}
