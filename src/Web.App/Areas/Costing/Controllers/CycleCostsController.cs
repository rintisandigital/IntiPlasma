using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Costing;
using Application.Cycles;
using Application.Cycles.GetById;
using Domain.Access;
using Domain.Costing.PlasmaSettlements;
using Domain.Partnership.Cycles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.Costing.Models;
using Web.App.Areas.Inventory;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Costing.Controllers;

/// <summary>
/// HPP per cycle: the list across cycles (final cost of closed cycles, running cost otherwise) and the breakdown of
/// one cycle per sapronak item, both exportable.
/// </summary>
[Area("Costing")]
[MenuAccess(MenuCodes.CostingCycleCosts)]
public sealed class CycleCostsController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetCycleCostsQuery, PagedList<CycleCostRowResponse>> costsQuery,
    IQueryHandler<GetCycleCostQuery, CycleCostResponse> costQuery,
    IQueryHandler<GetCycleByIdQuery, CycleResponse> cycleQuery) : AppController
{
    private const string MenuCode = MenuCodes.CostingCycleCosts;

    private static readonly ExportColumn<CycleCostRowResponse>[] Columns =
    [
        new("Cycle", c => c.CycleNumber, Width: 1.5f),
        new("Branch", c => c.BranchCode, Width: 0.7f),
        new("Coop", c => c.CoopCode, Width: 0.9f),
        new("Farmer", c => c.FarmerName, Width: 1.5f),
        new("Scheme", c => c.Scheme is null ? "Inti" : SettlementLines.Scheme(c.Scheme), Width: 1),
        new("Status", c => c.Status, Width: 0.8f),
        new("Chick-in", c => c.ChickInDate, ExportFormat.Date, 0.9f),
        new("Harvested birds", c => c.HarvestedBirds, ExportFormat.WholeNumber, 0.9f),
        new("Harvested kg", c => c.HarvestedWeightKg, ExportFormat.Number, 0.9f),
        new("DOC", c => c.DocCost, ExportFormat.Money, 1.2f),
        new("Feed", c => c.FeedCost, ExportFormat.Money, 1.2f),
        new("OVK", c => c.OvkCost, ExportFormat.Money, 1.1f),
        new("Total cost", c => c.TotalCost, ExportFormat.Money, 1.3f),
        new("Cost per kg", c => c.CostPerKg, ExportFormat.Money, 0.9f),
        new("Recognized", c => c.RecognizedCost, ExportFormat.Money, 1.2f),
        new("Adjustment", c => c.Adjustment, ExportFormat.Money, 1.1f),
        new("Plasma income", c => c.PlasmaIncome, ExportFormat.Money, 1.2f),
        new("Final", c => c.IsFinal, ExportFormat.Boolean, 0.6f)
    ];

    private static readonly ExportColumn<ConsumedInputResponse>[] InputColumns =
    [
        new("Item", i => i.ItemCode, Width: 1.5f),
        new("Category", i => EnumOptions.Label(i.Category), Width: 1),
        new("Quantity", i => i.Quantity, ExportFormat.Number, 1.2f),
        new("Cost", i => i.Cost, ExportFormat.Money, 1.5f),
        new("Cost per unit", i => i.Quantity == 0 ? null : decimal.Round(i.Cost / i.Quantity, 2), ExportFormat.Money, 1.2f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? branch, CycleStatus? status, DateOnly? from, DateOnly? to, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<CycleCostRowResponse>> result = await costsQuery.Handle(
            new GetCycleCostsQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, status, from, to),
            cancellationToken);

        return View(new ListViewModel<CycleCostRowResponse>
        {
            Rows = result.Value,
            Search = search,
            BranchOptions = branchFilter.Options,
            Filters = DocumentLists.DateFilters(from, to),
            FilterOptions = new Dictionary<string, IReadOnlyList<SelectListItem>> { ["status"] = EnumOptions.For(status) }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(
        string? format, string? search, string? branch, CycleStatus? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        List<string> filters = DocumentLists.Filters(branchFilter, status?.ToString(), from, to, formatter);
        filters.Add("Closed cycles show the final cost; the others the running cost of the sapronak consumed so far.");

        return await support.ExportAsync(format, "Cycle Cost (HPP)", "cycle-costs", filters, Columns,
            (paging, ct) => costsQuery.Handle(new GetCycleCostsQuery(paging, branchFilter.BranchId, status, from, to), ct),
            search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        [FromServices] InventoryOptions inventory,
        [FromServices] IQueryHandler<GetPlasmaSettlementsQuery, IReadOnlyList<PlasmaSettlementResponse>> settlementsQuery,
        CancellationToken cancellationToken)
    {
        Result<CycleResponse> cycle = await cycleQuery.Handle(new GetCycleByIdQuery(id), cancellationToken);
        if (cycle.IsFailure)
        {
            return cycle.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        Result<CycleCostResponse> cost = await costQuery.Handle(new GetCycleCostQuery(id), cancellationToken);
        if (cost.IsFailure)
        {
            return cost.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        Result<IReadOnlyList<PlasmaSettlementResponse>> settlements = await settlementsQuery.Handle(
            new GetPlasmaSettlementsQuery(null, null, id, null), cancellationToken);

        return View(new CycleCostDetailsViewModel(
            cost.Value,
            cycle.Value,
            settlements.IsSuccess ? settlements.Value.FirstOrDefault(s => s.Status != nameof(PlasmaSettlementStatus.Cancelled)) : null,
            await inventory.CoopWarehouseIdAsync(cycle.Value.CoopId, cancellationToken),
            CanPrint: await support.CanAsync(MenuCode, MenuRights.Export),
            CanCreateSettlement: await support.CanAsync(MenuCodes.CostingSettlements, MenuRights.Create)));
    }

    /// <summary>
    /// The cycle's sapronak consumed per item, with the cost summary in the header.
    /// </summary>
    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> DetailsExport(Guid id, string? format, CancellationToken cancellationToken)
    {
        Result<CycleCostResponse> result = await costQuery.Handle(new GetCycleCostQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        CycleCostResponse cost = result.Value;
        List<string> filters =
        [
            $"Cycle {cost.CycleNumber} ({EnumOptions.Label(cost.Status)}) — {(cost.IsFinal ? "final cost" : "running cost (estimate)")}",
            $"DOC {formatter.Money(cost.DocCost)} · Feed {formatter.Money(cost.FeedCost)} · OVK {formatter.Money(cost.OvkCost)} · Total {formatter.Money(cost.TotalCost)}",
            $"Cost per kg {(cost.CostPerKg is { } perKg ? formatter.Money(perKg) : "—")} · Cost per bird {(cost.CostPerBird is { } perBird ? formatter.Money(perBird) : "—")}",
            $"Recognized on invoices {formatter.Money(cost.RecognizedCost)}{(cost.Adjustment is { } adjustment ? $" · Closing adjustment {formatter.Money(adjustment)}" : string.Empty)}"
        ];
        if (cost.PlasmaIncome is { } income)
        {
            filters.Add($"Plasma income {formatter.Money(income)} · Total cost incl. plasma {formatter.Money(cost.TotalCostWithPlasma ?? cost.TotalCost)}");
        }

        return await support.ExportAsync(format, $"Cycle Cost {cost.CycleNumber}", $"cycle-cost-{cost.CycleNumber}", filters, InputColumns, cost.Inputs);
    }
}
