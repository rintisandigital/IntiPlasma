using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Warehouses;
using Application.Warehouses.Get;
using Domain.MasterData.Warehouses;
using SharedKernel;
using Web.App.Areas.Inventory.Models;
using Web.App.Infrastructure;

namespace Web.App.Areas.Inventory;

/// <summary>
/// Warehouse choices for the inventory forms: the active warehouses of the user's branches.
/// </summary>
public sealed class InventoryOptions(
    IQueryHandler<GetWarehousesQuery, PagedList<WarehouseResponse>> warehousesQuery,
    ItemOptions items,
    PageSupport support)
{
    private IReadOnlyList<WarehouseResponse>? _warehouses;

    public async Task<IReadOnlyList<WarehouseOption>> WarehousesAsync(Guid? branchId, CancellationToken cancellationToken) =>
        [.. (await AllAsync(cancellationToken))
            .Where(w => w.IsActive && (branchId is null || w.BranchId == branchId))
            .Select(w => new WarehouseOption(w.Id, Label(w), w.BranchId, w.Type))];

    /// <summary>
    /// The coop's own warehouse (GK-{coop code}), created with the coop.
    /// </summary>
    public async Task<Guid?> CoopWarehouseIdAsync(Guid coopId, CancellationToken cancellationToken) =>
        (await AllAsync(cancellationToken)).FirstOrDefault(w => w.CoopId == coopId)?.Id;

    public async Task<string?> LabelAsync(Guid? warehouseId, CancellationToken cancellationToken) =>
        (await AllAsync(cancellationToken)).FirstOrDefault(w => w.Id == warehouseId) is { } warehouse ? Label(warehouse) : null;

    /// <summary>
    /// Fills the choices of a transfer/return/mutation form: warehouses, attachments, and the label and units of each
    /// line's item (a re-rendered form keeps what the user chose).
    /// </summary>
    public async Task<StockMovementFormViewModel> PrepareAsync(
        StockMovementFormViewModel model, StockMovementKind kind, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        model.Kind = kind;
        model.Warehouses = await WarehousesAsync(null, cancellationToken);
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);

        foreach (StockLineInput line in model.Lines)
        {
            line.ItemLabel ??= await items.LabelAsync(line.ItemId, cancellationToken);
            line.Units = await items.UnitsAsync(line.ItemId, line.UomId, cancellationToken);
        }

        return model;
    }

    private static string Label(WarehouseResponse w) =>
        w.Type == nameof(WarehouseType.Coop)
            ? $"{w.Code} — {w.Name} (farm {w.CoopCode}, {w.BranchCode})"
            : $"{w.Code} — {w.Name} ({w.BranchCode})";

    private async Task<IReadOnlyList<WarehouseResponse>> AllAsync(CancellationToken cancellationToken)
    {
        if (_warehouses is not null)
        {
            return _warehouses;
        }

        var all = new List<WarehouseResponse>();
        bool more = true;
        int page = 1;
        while (more)
        {
            Result<PagedList<WarehouseResponse>> result = await warehousesQuery.Handle(
                new GetWarehousesQuery(new PageRequest(page, PageRequest.MaxPageSize), null, null), cancellationToken);

            if (result.IsSuccess)
            {
                all.AddRange(result.Value.Items);
            }

            more = result.IsSuccess && result.Value.HasNextPage && result.Value.Items.Count > 0;
            page++;
        }

        _warehouses = all;
        return all;
    }
}
