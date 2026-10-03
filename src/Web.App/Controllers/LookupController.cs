using System.Globalization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Farmers;
using Application.Finance.Accounts;
using Application.Farmers.Get;
using Application.Items;
using Application.Inventory;
using Application.Items.Get;
using Application.Items.GetById;
using Application.Procurement;
using Application.Vendors;
using Application.Vendors.Get;
using Domain.Finance.Accounts;
using Domain.MasterData.Farmers;
using Domain.MasterData.Items;
using Domain.Procurement.PurchaseOrders;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Controllers;

/// <summary>
/// Search endpoints for Tom-Select dropdowns (<c>select[data-lookup]</c>, ~/js/lookup.js): id + label, at most
/// 20 matches. Results go through the same use cases as the lists, so branch scope applies.
/// </summary>
[AuthenticatedOnly]
public sealed class LookupController : AppController
{
    private const int MaxResults = 20;

    [HttpGet]
    public async Task<IActionResult> Items(
        string? q,
        ItemCategory? category,
        [FromServices] IQueryHandler<GetItemsQuery, PagedList<ItemResponse>> query,
        CancellationToken cancellationToken)
    {
        Result<PagedList<ItemResponse>> result = await query.Handle(
            new GetItemsQuery(new PageRequest(1, MaxResults, q), category), cancellationToken);

        return Json(result.IsFailure
            ? []
            : result.Value.Items.Where(i => i.IsActive).Select(i => new LookupItem(i.Id, $"{i.Code} — {i.Name} ({i.BaseUomCode})")));
    }

    [HttpGet]
    public async Task<IActionResult> Farmers(
        string? q,
        Guid? branchId,
        FarmerType? type,
        [FromServices] IQueryHandler<GetFarmersQuery, PagedList<FarmerResponse>> query,
        CancellationToken cancellationToken)
    {
        Result<PagedList<FarmerResponse>> result = await query.Handle(
            new GetFarmersQuery(new PageRequest(1, MaxResults, q), branchId, type), cancellationToken);

        return Json(result.IsFailure
            ? []
            : result.Value.Items.Where(f => f.IsActive).Select(f => new LookupItem(f.Id, $"{f.Code} — {f.Name} ({f.Type}, {f.BranchCode})")));
    }

    /// <summary>
    /// Postable, active accounts of the chart of accounts (journal lines, mappings, cash/bank accounts).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Accounts(
        string? q,
        AccountType? type,
        [FromServices] IQueryHandler<GetAccountsQuery, IReadOnlyList<AccountResponse>> query,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<AccountResponse>> result = await query.Handle(new GetAccountsQuery(type, q, PostableOnly: true), cancellationToken);

        return Json(result.IsFailure
            ? []
            : result.Value.Take(MaxResults).Select(a => new LookupItem(a.Id, $"{a.Code} — {a.Name}")));
    }

    [HttpGet]
    public async Task<IActionResult> Vendors(
        string? q,
        [FromServices] IQueryHandler<GetVendorsQuery, PagedList<VendorResponse>> query,
        CancellationToken cancellationToken)
    {
        Result<PagedList<VendorResponse>> result = await query.Handle(new GetVendorsQuery(new PageRequest(1, MaxResults, q)), cancellationToken);

        return Json(result.IsFailure
            ? []
            : result.Value.Items.Where(v => v.IsActive).Select(v => new LookupItem(v.Id, $"{v.Code} — {v.Name}")));
    }

    /// <summary>
    /// Active sapronak items (DOC, feed, OVK) — the items a purchase order can buy.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> PurchasableItems(
        string? q,
        [FromServices] IQueryHandler<GetItemsQuery, PagedList<ItemResponse>> query,
        CancellationToken cancellationToken)
    {
        Result<PagedList<ItemResponse>> result = await query.Handle(
            new GetItemsQuery(new PageRequest(1, PageRequest.MaxPageSize, q), null), cancellationToken);

        return Json(result.IsFailure
            ? []
            : result.Value.Items
                .Where(i => i.IsActive && i.Category is nameof(ItemCategory.Doc) or nameof(ItemCategory.Feed) or nameof(ItemCategory.Ovk))
                .Take(MaxResults)
                .Select(i => new LookupItem(i.Id, $"{i.Code} — {i.Name} ({i.BaseUomCode})")));
    }

    /// <summary>
    /// Units an item can be entered in (base unit first, then its conversions) and its default tax code.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ItemUnits(
        Guid itemId,
        [FromServices] IQueryHandler<GetItemByIdQuery, ItemResponse> query,
        CancellationToken cancellationToken)
    {
        Result<ItemResponse> result = await query.Handle(new GetItemByIdQuery(itemId), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        ItemResponse item = result.Value;
        UnitItem[] units =
        [
            new(item.BaseUomId, item.BaseUomCode, 1),
            .. item.Conversions.Select(c => new UnitItem(c.UomId, $"{c.UomCode} (= {c.Factor:0.######} {item.BaseUomCode})", c.Factor))
        ];

        return Json(new { units, taxCodeId = item.TaxCodeId });
    }

    /// <summary>
    /// Items with stock in a warehouse, labelled with the quantity on hand (transfers and returns).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> StockItems(
        string? q,
        Guid? warehouseId,
        [FromServices] IQueryHandler<GetStockBalancesQuery, PagedList<StockBalanceResponse>> query,
        CancellationToken cancellationToken)
    {
        if (warehouseId is null)
        {
            return Json(Array.Empty<LookupItem>());
        }

        Result<PagedList<StockBalanceResponse>> result = await query.Handle(
            new GetStockBalancesQuery(new PageRequest(1, MaxResults, q), null, warehouseId, null, null, IncludeEmpty: false),
            cancellationToken);

        return Json(result.IsFailure
            ? []
            : result.Value.Items.Where(s => s.Quantity > 0).Select(s => new LookupItem(
                s.ItemId, $"{s.ItemCode} — {s.ItemName} (on hand {s.Quantity.ToString("#,##0.###", CultureInfo.InvariantCulture)} {s.BaseUomCode})")));
    }

    /// <summary>
    /// Approved or partially received purchase orders (goods can still be received).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ReceivablePurchaseOrders(
        string? q,
        [FromServices] IQueryHandler<GetPurchaseOrdersQuery, PagedList<PurchaseOrderResponse>> query,
        CancellationToken cancellationToken)
    {
        var orders = new List<PurchaseOrderResponse>();
        foreach (PurchaseOrderStatus status in new[] { PurchaseOrderStatus.Approved, PurchaseOrderStatus.PartiallyReceived })
        {
            Result<PagedList<PurchaseOrderResponse>> result = await query.Handle(
                new GetPurchaseOrdersQuery(new PageRequest(1, MaxResults, q), null, null, status), cancellationToken);
            if (result.IsSuccess)
            {
                orders.AddRange(result.Value.Items);
            }
        }

        return Json(orders
            .OrderByDescending(o => o.OrderDate)
            .Take(MaxResults)
            .Select(o => new LookupItem(o.Id, $"{o.Number} — {o.VendorName} ({o.BranchCode}, {o.OrderDate:dd/MM/yyyy})")));
    }

    public sealed record LookupItem(Guid Value, string Text);

    public sealed record UnitItem(Guid Value, string Text, decimal Factor);
}
