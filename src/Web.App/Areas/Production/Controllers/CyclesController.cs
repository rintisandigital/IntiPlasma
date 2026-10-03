using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Costing;
using Application.Cycles;
using Application.Cycles.Cancel;
using Application.Cycles.Get;
using Application.Cycles.GetById;
using Application.Cycles.Plan;
using Application.Cycles.Start;
using Application.Documents;
using Application.Inventory;
using Application.Production;
using Domain.Access;
using Domain.Documents.Attachments;
using Domain.MasterData.Items;
using Domain.Partnership.Cycles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.Inventory;
using Web.App.Areas.MasterData.Models;
using Web.App.Areas.Production.Documents;
using Web.App.Areas.Production.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;
using Web.App.Infrastructure.Workflow;

namespace Web.App.Areas.Production.Controllers;

/// <summary>
/// Production cycles: plan (coop + contract) → DOC into the coop warehouse (stock transfer) → chick-in → daily
/// recordings and harvests → close (frozen performance and cost) → settlement. Planned cycles can be cancelled.
/// </summary>
[Area("Production")]
[MenuAccess(MenuCodes.ProductionCycles)]
public sealed class CyclesController(
    PageSupport support,
    InventoryOptions inventory,
    DisplayFormatter formatter,
    IQueryHandler<GetCyclesQuery, PagedList<CycleResponse>> cyclesQuery,
    IQueryHandler<GetCycleByIdQuery, CycleResponse> cycleQuery,
    IQueryHandler<GetStockBalancesQuery, PagedList<StockBalanceResponse>> stockQuery) : AppController
{
    private const string MenuCode = MenuCodes.ProductionCycles;
    private const string DocumentType = "Cycle";

    private static readonly ExportColumn<CycleResponse>[] Columns =
    [
        new("Number", c => c.Number, Width: 1.6f),
        new("Status", c => c.Status, Width: 0.9f),
        new("Branch", c => c.BranchCode, Width: 0.7f),
        new("Farmer", c => $"{c.FarmerCode} — {c.FarmerName}", Width: 2.2f),
        new("Type", c => c.FarmerType, Width: 0.7f),
        new("Coop", c => $"{c.CoopCode} — {c.CoopName}", Width: 2),
        new("Contract", c => c.ContractCode, Width: 1),
        new("Chick-in", c => c.ChickInDate ?? c.PlannedChickInDate, ExportFormat.Date, 1),
        new("Initial population", c => c.InitialPopulation ?? c.PlannedPopulation, ExportFormat.WholeNumber, 1),
        new("Mortality", c => c.TotalMortality, ExportFormat.WholeNumber, 0.8f),
        new("Culling", c => c.TotalCulling, ExportFormat.WholeNumber, 0.8f),
        new("Harvested birds", c => c.HarvestedBirds, ExportFormat.WholeNumber, 1),
        new("Harvested kg", c => c.HarvestedWeightKg, ExportFormat.Number, 1),
        new("Population", c => c.CurrentPopulation, ExportFormat.WholeNumber, 0.9f),
        new("Closed", c => c.ClosedDate, ExportFormat.Date, 1)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? branch, CycleStatus? status, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<CycleResponse>> result = await cyclesQuery.Handle(
            new GetCyclesQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, null, null, status),
            cancellationToken);

        return View(new ListViewModel<CycleResponse>
        {
            Rows = result.Value,
            Search = search,
            BranchOptions = branchFilter.Options,
            FilterOptions = new Dictionary<string, IReadOnlyList<SelectListItem>> { ["status"] = EnumOptions.For(status) }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, string? search, string? branch, CycleStatus? status, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        List<string> filters = [branchFilter.Description];
        if (status is not null)
        {
            filters.Add($"Status: {status}");
        }

        return await support.ExportAsync(format, "Production Cycles", "cycles", filters, Columns,
            (paging, ct) => cyclesQuery.Handle(new GetCyclesQuery(paging, branchFilter.BranchId, null, null, status), ct),
            search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        string? tab,
        [FromServices] IQueryHandler<GetCyclePerformanceQuery, CyclePerformanceResponse> performanceQuery,
        [FromServices] IQueryHandler<GetCycleCostQuery, CycleCostResponse> costQuery,
        [FromServices] IQueryHandler<GetDailyRecordingsQuery, IReadOnlyList<DailyRecordingResponse>> recordingsQuery,
        CancellationToken cancellationToken)
    {
        Result<CycleResponse> result = await cycleQuery.Handle(new GetCycleByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        CycleResponse cycle = result.Value;
        Guid? warehouseId = await inventory.CoopWarehouseIdAsync(cycle.CoopId, cancellationToken);
        Result<CyclePerformanceResponse> performance = await performanceQuery.Handle(new GetCyclePerformanceQuery(id), cancellationToken);
        Result<CycleCostResponse> cost = await costQuery.Handle(new GetCycleCostQuery(id), cancellationToken);
        Result<IReadOnlyList<DailyRecordingResponse>> recordings = await recordingsQuery.Handle(new GetDailyRecordingsQuery(id, null, null), cancellationToken);

        return View(new CycleDetailsViewModel
        {
            Cycle = cycle,
            Performance = performance.IsSuccess ? performance.Value : null,
            Cost = cost.IsSuccess ? cost.Value : null,
            Recordings = recordings.IsSuccess ? recordings.Value : [],
            CoopStock = await CoopStockAsync(warehouseId, cancellationToken),
            CoopWarehouseId = warehouseId,
            Attachments = await support.AttachmentsAsync(cycle.Documents, cancellationToken),
            Tab = tab ?? "overview",
            CanEdit = await support.CanAsync(MenuCode, MenuRights.Edit),
            CanRecord = await support.CanAsync(MenuCodes.ProductionRecordings, MenuRights.Create),
            CanHarvest = await support.CanAsync(MenuCodes.ProductionHarvests, MenuRights.Create),
            CanPrint = await support.CanAsync(MenuCode, MenuRights.Export),
            CanTransfer = await support.CanAsync(MenuCodes.InventoryStockTransfers, MenuRights.Create)
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public IActionResult Create() =>
        View("Plan", new PlanCycleFormViewModel { PlannedChickInDate = formatter.Today() });

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        PlanCycleFormViewModel model,
        [FromServices] ICommandHandler<PlanCycleCommand, PlanCycleResponse> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<PlanCycleResponse> result = await handler.Handle(
                new PlanCycleCommand(model.CoopId!.Value, model.ContractId, model.PlannedChickInDate!.Value, model.PlannedPopulation!.Value, model.Notes),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Cycle {result.Value.Number} has been planned. Transfer DOC to the coop warehouse, then record the chick-in.");
                return RedirectToAction(nameof(Details), new { id = result.Value.Id });
            }

            AddErrors(result.Error);
        }

        return View("Plan", model);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> ChickIn(Guid id, CancellationToken cancellationToken)
    {
        var model = new ChickInFormViewModel { ChickInDate = formatter.Today() };
        if (!await WithCycleAsync(id, model, cancellationToken))
        {
            return NotFound();
        }

        model.Lines = [.. model.DocStock.Select(s => new ChickInLineInput { ItemId = s.ItemId, Quantity = (int)Math.Floor(s.Quantity) })];
        return View(model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("chick-in")]
    public async Task<IActionResult> ChickIn(
        Guid id,
        ChickInFormViewModel model,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<StartCycleCommand, int> handler,
        CancellationToken cancellationToken)
    {
        ChickInLine[] lines = [.. model.Lines.Where(l => l.Quantity > 0).Select(l => new ChickInLine(l.ItemId, l.Quantity!.Value))];
        if (lines.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Enter the number of birds placed.");
        }

        if (ModelState.IsValid)
        {
            int population = 0;
            Result result = await workflow.ExecuteAsync(
                new WorkflowActionRequest(DocumentType, id, "chick-in"),
                async ct =>
                {
                    Result<int> started = await handler.Handle(new StartCycleCommand(id, model.ChickInDate!.Value, lines, model.Documents), ct);
                    population = started.IsSuccess ? started.Value : 0;
                    return started.IsSuccess ? Result.Success() : Result.Failure(started.Error);
                },
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Chick-in recorded: {formatter.Number(population)} birds placed. The cycle is now active.");
                return RedirectToAction(nameof(Details), new { id });
            }

            AddErrors(result.Error);
        }

        await WithCycleAsync(id, model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        string? reason,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<CancelCycleCommand> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            NotifyError("Enter the reason for cancelling.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "cancel"),
            ct => handler.Handle(new CancelCycleCommand(id, reason), ct),
            cancellationToken);

        return AfterAction(id, result, "The cycle has been cancelled.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("close")]
    public async Task<IActionResult> Close(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<CloseCycleCommand, Domain.Partnership.Cycles.CyclePerformance> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "close"),
            async ct =>
            {
                Result<Domain.Partnership.Cycles.CyclePerformance> closed = await handler.Handle(new CloseCycleCommand(id), ct);
                return closed.IsSuccess ? Result.Success() : Result.Failure(closed.Error);
            },
            cancellationToken);

        return AfterAction(id, result, "The cycle has been closed; its performance and cost are now final.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Documents(
        Guid id,
        List<Guid> documents,
        [FromServices] ICommandHandler<SetDocumentsCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new SetDocumentsCommand(AttachmentOwnerTypes.Cycle, id, documents), cancellationToken);

        if (result.IsSuccess)
        {
            NotifySuccess("The attachments have been saved.");
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Details), new { id, tab = "attachments" });
    }

    /// <summary>
    /// Cycle summary (ringkasan siklus): population, harvests, performance and cost — final once the cycle is closed.
    /// </summary>
    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Print(
        Guid id,
        [FromServices] IQueryHandler<GetCyclePerformanceQuery, CyclePerformanceResponse> performanceQuery,
        [FromServices] IQueryHandler<GetCycleCostQuery, CycleCostResponse> costQuery,
        CancellationToken cancellationToken)
    {
        Result<CycleResponse> result = await cycleQuery.Handle(new GetCycleByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        CycleResponse cycle = result.Value;
        Result<CyclePerformanceResponse> performance = await performanceQuery.Handle(new GetCyclePerformanceQuery(id), cancellationToken);
        Result<CycleCostResponse> cost = await costQuery.Handle(new GetCycleCostQuery(id), cancellationToken);

        ExportHeader header = await support.Exports.HeaderAsync(
            cycle.ClosedDate is null ? "Cycle Summary (interim)" : "Cycle Closing Summary",
            $"cycle-{cycle.Number}",
            [$"No. {cycle.Number}", $"Status: {cycle.Status}"]);

        return support.Exports.Document(header, container => CycleSummaryPdf.Compose(
            container, cycle, performance.IsSuccess ? performance.Value : null, cost.IsSuccess ? cost.Value : null, formatter));
    }

    private RedirectToActionResult AfterAction(Guid id, Result result, string successMessage)
    {
        if (result.IsSuccess)
        {
            NotifySuccess(successMessage);
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<IReadOnlyList<StockBalanceResponse>> CoopStockAsync(Guid? warehouseId, CancellationToken cancellationToken)
    {
        if (warehouseId is null)
        {
            return [];
        }

        Result<PagedList<StockBalanceResponse>> stock = await stockQuery.Handle(
            new GetStockBalancesQuery(new PageRequest(1, PageRequest.MaxPageSize), null, warehouseId, null, null, IncludeEmpty: false),
            cancellationToken);

        return stock.IsSuccess ? stock.Value.Items : [];
    }

    private async Task<bool> WithCycleAsync(Guid id, ChickInFormViewModel model, CancellationToken cancellationToken)
    {
        Result<CycleResponse> cycle = await cycleQuery.Handle(new GetCycleByIdQuery(id), cancellationToken);
        if (cycle.IsFailure)
        {
            return false;
        }

        model.Cycle = cycle.Value;
        model.CoopWarehouseId = await inventory.CoopWarehouseIdAsync(cycle.Value.CoopId, cancellationToken);
        model.DocStock = [.. (await CoopStockAsync(model.CoopWarehouseId, cancellationToken)).Where(s => s.ItemCategory == nameof(ItemCategory.Doc))];
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);
        return true;
    }
}
