using System.Globalization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Contracts;
using Application.Contracts.Get;
using Application.Coops;
using Application.Coops.Get;
using Application.Coops.GetById;
using Application.Costing;
using Application.Cycles;
using Application.Cycles.Get;
using Application.Customers;
using Application.Customers.Get;
using Application.Farmers;
using Application.Finance.Accounts;
using Application.Farmers.Get;
using Application.Farmers.GetById;
using Application.Users.FieldOfficers;
using Application.Items;
using Application.Inventory;
using Application.Items.Get;
using Application.Items.GetById;
using Application.Procurement;
using Application.Sales;
using Application.Vendors;
using Application.Vendors.Get;
using Domain.Finance.Accounts;
using Domain.MasterData.Farmers;
using Domain.Partnership.Contracts;
using Domain.Partnership.Cycles;
using Domain.MasterData.Items;
using Domain.Procurement.PurchaseOrders;
using Domain.Sales.SalesOrders;
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
    /// Field officers (PPL) a farmer or farm can be assigned to: of the branch, or of the farmer's branch (new farm).
    /// Nothing until the branch is known.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> FieldOfficers(
        string? q,
        Guid? branchId,
        Guid? farmerId,
        [FromServices] IQueryHandler<GetFieldOfficersQuery, IReadOnlyList<FieldOfficerResponse>> query,
        [FromServices] IQueryHandler<GetFarmerByIdQuery, FarmerResponse> farmerQuery,
        CancellationToken cancellationToken)
    {
        if (branchId is null && farmerId is { } id)
        {
            Result<FarmerResponse> farmer = await farmerQuery.Handle(new GetFarmerByIdQuery(id), cancellationToken);
            branchId = farmer.IsSuccess ? farmer.Value.BranchId : null;
        }

        if (branchId is null)
        {
            return Json(Array.Empty<LookupItem>());
        }

        Result<IReadOnlyList<FieldOfficerResponse>> result =
            await query.Handle(new GetFieldOfficersQuery(branchId), cancellationToken);

        return Json(result.IsFailure
            ? []
            : result.Value
                .Where(o => string.IsNullOrWhiteSpace(q) ||
                            o.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            o.Email.Contains(q, StringComparison.OrdinalIgnoreCase))
                .Take(MaxResults)
                .Select(o => new LookupItem(o.Id, $"{o.Name} ({o.Email})")));
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

    /// <summary>
    /// Active coops without an open cycle — the coops a new cycle can be planned in.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> PlannableCoops(
        string? q,
        [FromServices] IQueryHandler<GetCoopsQuery, PagedList<CoopResponse>> query,
        CancellationToken cancellationToken)
    {
        Result<PagedList<CoopResponse>> result = await query.Handle(
            new GetCoopsQuery(new PageRequest(1, PageRequest.MaxPageSize, q), null, null), cancellationToken);

        return Json(result.IsFailure
            ? []
            : result.Value.Items
                .Where(c => c.IsActive && c.OpenCycleId is null)
                .Take(MaxResults)
                .Select(c => new LookupItem(c.Id, $"{c.Code} — {c.Name} · {c.FarmerName} ({c.FarmerType}, {c.BranchCode}, cap. {c.Capacity:#,##0})")));
    }

    /// <summary>
    /// Active contracts of the coop's branch; none for an inti coop (inti cycles have no contract).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CoopContracts(
        string? q,
        Guid? coopId,
        [FromServices] IQueryHandler<GetCoopByIdQuery, CoopResponse> coopQuery,
        [FromServices] IQueryHandler<GetContractsQuery, PagedList<ContractResponse>> contractsQuery,
        CancellationToken cancellationToken)
    {
        if (coopId is not Guid id || await coopQuery.Handle(new GetCoopByIdQuery(id), cancellationToken) is not { IsSuccess: true } coop ||
            coop.Value.FarmerType == nameof(FarmerType.Inti))
        {
            return Json(Array.Empty<LookupItem>());
        }

        Result<PagedList<ContractResponse>> result = await contractsQuery.Handle(
            new GetContractsQuery(new PageRequest(1, MaxResults, q), coop.Value.BranchId, ContractStatus.Active), cancellationToken);

        return Json(result.IsFailure
            ? []
            : result.Value.Items.Select(c => new LookupItem(c.Id, $"{c.Code} — {c.Name} ({EnumLabel(c.Scheme)})")));
    }

    /// <summary>
    /// Cycles that accept daily recordings and harvests (active or harvesting).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> RecordableCycles(
        string? q,
        [FromServices] IQueryHandler<GetCyclesQuery, PagedList<CycleResponse>> query,
        CancellationToken cancellationToken)
    {
        var cycles = new List<CycleResponse>();
        foreach (CycleStatus status in new[] { CycleStatus.Active, CycleStatus.Harvesting })
        {
            Result<PagedList<CycleResponse>> result = await query.Handle(
                new GetCyclesQuery(new PageRequest(1, MaxResults, q), null, null, null, status), cancellationToken);
            if (result.IsSuccess)
            {
                cycles.AddRange(result.Value.Items);
            }
        }

        return Json(cycles
            .OrderBy(c => c.CoopCode)
            .Take(MaxResults)
            .Select(c => new LookupItem(c.Id, CycleLabel(c))));
    }

    [HttpGet]
    public async Task<IActionResult> Customers(
        string? q,
        [FromServices] IQueryHandler<GetCustomersQuery, PagedList<CustomerResponse>> query,
        CancellationToken cancellationToken)
    {
        Result<PagedList<CustomerResponse>> result = await query.Handle(new GetCustomersQuery(new PageRequest(1, MaxResults, q)), cancellationToken);

        return Json(result.IsFailure
            ? []
            : result.Value.Items.Where(c => c.IsActive).Select(c => new LookupItem(c.Id, $"{c.Code} — {c.Name}")));
    }

    /// <summary>
    /// Approved or partially delivered sales orders (birds can still be delivered).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DeliverableSalesOrders(
        string? q,
        [FromServices] IQueryHandler<GetSalesOrdersQuery, PagedList<SalesOrderResponse>> query,
        CancellationToken cancellationToken)
    {
        var orders = new List<SalesOrderResponse>();
        foreach (SalesOrderStatus status in new[] { SalesOrderStatus.Approved, SalesOrderStatus.PartiallyDelivered })
        {
            Result<PagedList<SalesOrderResponse>> result = await query.Handle(
                new GetSalesOrdersQuery(new PageRequest(1, MaxResults, q), null, null, status, null, null), cancellationToken);
            if (result.IsSuccess)
            {
                orders.AddRange(result.Value.Items);
            }
        }

        return Json(orders
            .OrderByDescending(o => o.OrderDate)
            .Take(MaxResults)
            .Select(o => new LookupItem(o.Id, $"{o.Number} — {o.CustomerName} ({o.BranchCode}, {o.Birds - o.DeliveredBirds:#,##0} birds open)")));
    }

    /// <summary>
    /// Closed plasma cycles without an active settlement.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SettleableCycles(
        string? q,
        [FromServices] IQueryHandler<GetSettleableCyclesQuery, IReadOnlyList<SettleableCycleResponse>> query,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<SettleableCycleResponse>> result = await query.Handle(new GetSettleableCyclesQuery(q), cancellationToken);

        return Json(result.IsFailure
            ? []
            : result.Value.Take(MaxResults).Select(c => new LookupItem(
                c.CycleId, $"{c.CycleNumber} — {c.CoopCode} · {c.FarmerName} ({c.BranchCode}, {EnumLabel(c.Scheme)})")));
    }

    public static string CycleLabel(CycleResponse cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        return $"{cycle.Number} — {cycle.CoopCode} {cycle.CoopName} · {cycle.FarmerName} ({cycle.BranchCode})";
    }

    private static string EnumLabel(string value) => System.Text.RegularExpressions.Regex.Replace(value, "(?<=[a-z])(?=[A-Z])", " ");

    public sealed record LookupItem(Guid Value, string Text);

    public sealed record UnitItem(Guid Value, string Text, decimal Factor);
}
