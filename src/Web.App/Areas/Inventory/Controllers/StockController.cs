using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Inventory;
using Domain.Access;
using Domain.MasterData.Items;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.Inventory.Models;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Inventory.Controllers;

/// <summary>
/// Stock on hand per warehouse and item (moving average cost) and the stock card (kartu stok) of one item in one
/// warehouse over a period.
/// </summary>
[Area("Inventory")]
[MenuAccess(MenuCodes.InventoryStock)]
public sealed class StockController(
    PageSupport support,
    InventoryOptions options,
    ItemOptions items,
    DisplayFormatter formatter,
    IQueryHandler<GetStockBalancesQuery, PagedList<StockBalanceResponse>> balancesQuery,
    IQueryHandler<GetStockCardQuery, StockCardResponse> cardQuery) : AppController
{
    private const string MenuCode = MenuCodes.InventoryStock;

    private static readonly ExportColumn<StockBalanceResponse>[] BalanceColumns =
    [
        new("Warehouse", s => s.WarehouseCode, Width: 1.3f),
        new("Type", s => s.WarehouseType, Width: 0.8f),
        new("Item", s => s.ItemCode, Width: 1.2f),
        new("Name", s => s.ItemName, Width: 3),
        new("Category", s => EnumOptions.Label(s.ItemCategory), Width: 1),
        new("Quantity", s => s.Quantity, ExportFormat.Number, 1.2f),
        new("Unit", s => s.BaseUomCode, Width: 0.6f),
        new("Average cost", s => s.AverageCost, ExportFormat.Money, 1.3f),
        new("Value", s => s.Value, ExportFormat.Money, 1.5f)
    ];

    private static readonly ExportColumn<StockCardLine>[] CardColumns =
    [
        new("Date", l => l.Date, ExportFormat.Date, 1),
        new("Movement", l => EnumOptions.Label(l.Type), Width: 1.3f),
        new("Document", l => l.SourceNumber, Width: 1.5f),
        new("Cycle", l => l.CycleNumber, Width: 1.3f),
        new("Quantity", l => l.Quantity, ExportFormat.Number, 1.1f),
        new("Unit cost", l => l.UnitCost, ExportFormat.Money, 1.2f),
        new("Value", l => l.Value, ExportFormat.Money, 1.3f),
        new("Balance qty", l => l.BalanceQuantity, ExportFormat.Number, 1.1f),
        new("Balance value", l => l.BalanceValue, ExportFormat.Money, 1.4f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? branch, Guid? warehouseId, ItemCategory? category, bool? includeEmpty, int? page,
        CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<StockBalanceResponse>> result = await balancesQuery.Handle(
            new GetStockBalancesQuery(
                new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, warehouseId, null, category, includeEmpty == true),
            cancellationToken);

        return View(new ListViewModel<StockBalanceResponse>
        {
            Rows = result.Value,
            Search = search,
            BranchOptions = branchFilter.Options,
            Filters = new Dictionary<string, string?> { ["includeEmpty"] = includeEmpty == true ? "true" : null },
            FilterOptions = new Dictionary<string, IReadOnlyList<SelectListItem>>
            {
                ["category"] = EnumOptions.For(category),
                ["warehouse"] = await WarehouseItemsAsync(branchFilter.BranchId, warehouseId, cancellationToken)
            }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(
        string? format, string? search, string? branch, Guid? warehouseId, ItemCategory? category, bool? includeEmpty,
        CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        List<string> filters = [branchFilter.Description];
        if (await options.LabelAsync(warehouseId, cancellationToken) is { } warehouse)
        {
            filters.Add($"Warehouse: {warehouse}");
        }

        if (category is not null)
        {
            filters.Add($"Category: {EnumOptions.Label(category.Value.ToString())}");
        }

        return await support.ExportAsync(format, "Stock Balance", "stock-balance", filters, BalanceColumns,
            (paging, ct) => balancesQuery.Handle(
                new GetStockBalancesQuery(paging, branchFilter.BranchId, warehouseId, null, category, includeEmpty == true), ct),
            search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Card(Guid? warehouseId, Guid? itemId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        DateOnly today = formatter.Today();
        DateOnly start = from ?? new DateOnly(today.Year, today.Month, 1);
        DateOnly end = to ?? today;
        StockCardResponse? card = null;

        if (warehouseId is Guid warehouse && itemId is Guid item)
        {
            Result<StockCardResponse> result = await cardQuery.Handle(new GetStockCardQuery(warehouse, item, start, end), cancellationToken);
            if (result.IsSuccess)
            {
                card = result.Value;
            }
            else
            {
                NotifyError(result.Error.Description);
            }
        }

        string? itemLabel = await items.LabelAsync(itemId, cancellationToken);

        return View(new StockCardViewModel
        {
            WarehouseId = warehouseId,
            ItemId = itemId,
            ItemLabel = itemLabel,
            From = start,
            To = end,
            Card = card,
            WarehouseLabel = await options.LabelAsync(warehouseId, cancellationToken),
            BaseUomCode = await BaseUnitAsync(itemId, cancellationToken),
            Warehouses = await WarehouseItemsAsync(null, warehouseId, cancellationToken)
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> CardExport(
        string? format, Guid warehouseId, Guid itemId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        Result<StockCardResponse> result = await cardQuery.Handle(new GetStockCardQuery(warehouseId, itemId, from, to), cancellationToken);

        if (result.IsFailure)
        {
            NotifyError(result.Error.Description);
            return RedirectToAction(nameof(Card), new { warehouseId, itemId, from, to });
        }

        StockCardResponse card = result.Value;
        List<string> filters =
        [
            $"Warehouse: {await options.LabelAsync(warehouseId, cancellationToken)}",
            $"Item: {await items.LabelAsync(itemId, cancellationToken)}",
            $"Period: {formatter.Date(from)} – {formatter.Date(to)}",
            $"Opening: {formatter.Number(card.OpeningQuantity, 3)} / Rp {formatter.Number(card.OpeningValue)}",
            $"Closing: {formatter.Number(card.ClosingQuantity, 3)} / Rp {formatter.Number(card.ClosingValue)}"
        ];

        return await support.ExportAsync(format, "Stock Card", "stock-card", filters, CardColumns, card.Lines);
    }

    private async Task<string?> BaseUnitAsync(Guid? itemId, CancellationToken cancellationToken)
    {
        IReadOnlyList<SelectListItem> units = await items.UnitsAsync(itemId, null, cancellationToken);
        return units.Count == 0 ? null : units[0].Text;
    }

    private async Task<IReadOnlyList<SelectListItem>> WarehouseItemsAsync(Guid? branchId, Guid? selected, CancellationToken cancellationToken) =>
        [.. (await options.WarehousesAsync(branchId, cancellationToken)).Select(w => new SelectListItem(w.Label, w.Id.ToString(), w.Id == selected))];
}
