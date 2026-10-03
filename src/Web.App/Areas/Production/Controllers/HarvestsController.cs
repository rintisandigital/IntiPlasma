using Application.Abstractions.Messaging;
using Application.Cycles;
using Application.Cycles.GetById;
using Application.Documents;
using Application.Production;
using Domain.Access;
using Domain.Documents.Attachments;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Production.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Production.Controllers;

/// <summary>
/// Panen per truk: birds caught and weighed (with the weighbridge ticket). The first harvest moves the cycle to
/// Harvesting; harvested birds are sold through delivery orders.
/// </summary>
[Area("Production")]
[MenuAccess(MenuCodes.ProductionHarvests)]
public sealed class HarvestsController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetCycleByIdQuery, CycleResponse> cycleQuery,
    IQueryHandler<GetCyclePerformanceQuery, CyclePerformanceResponse> performanceQuery) : AppController
{
    private const string MenuCode = MenuCodes.ProductionHarvests;

    private static readonly ExportColumn<HarvestResponse>[] Columns =
    [
        new("Date", h => h.Date, ExportFormat.Date, 1),
        new("Age (days)", h => h.AgeDays, ExportFormat.WholeNumber, 0.8f),
        new("Birds", h => h.Birds, ExportFormat.WholeNumber, 1),
        new("Weight (kg)", h => h.WeightKg, ExportFormat.Number, 1.2f),
        new("Average (kg)", h => h.AverageWeightKg, ExportFormat.Number, 1),
        new("Truck / notes", h => h.Notes, Width: 3)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(Guid? cycleId, CancellationToken cancellationToken) =>
        View(await WorkspaceAsync(cycleId, new HarvestFormViewModel { CycleId = cycleId, Date = formatter.Today() }, cancellationToken));

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, Guid cycleId, CancellationToken cancellationToken)
    {
        CycleWorkspaceViewModel workspace = await WorkspaceAsync(cycleId, new HarvestFormViewModel(), cancellationToken);

        return await support.ExportAsync(format, "Harvests", $"harvests-{workspace.Cycle?.CoopCode}",
            [$"Cycle: {workspace.CycleLabel}"], Columns, workspace.Harvests);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Harvest")] HarvestFormViewModel model,
        [FromServices] ICommandHandler<RecordHarvestCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new RecordHarvestCommand(model.CycleId!.Value, model.Date!.Value, model.Birds!.Value, model.WeightKg!.Value, model.Notes, model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Harvest of {formatter.Number(model.Birds!.Value)} birds / {formatter.Number(model.WeightKg!.Value, 2)} kg has been recorded.");
                return RedirectToAction(nameof(Index), new { cycleId = model.CycleId });
            }

            AddErrors(result.Error);
        }

        return View("Index", await WorkspaceAsync(model.CycleId, model, cancellationToken));
    }

    /// <summary>
    /// Replaces the attachments (weighbridge ticket, truck photos) of one harvest.
    /// </summary>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Documents(
        Guid id,
        Guid cycleId,
        List<Guid> documents,
        [FromServices] ICommandHandler<SetDocumentsCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new SetDocumentsCommand(AttachmentOwnerTypes.Harvest, id, documents), cancellationToken);

        if (result.IsSuccess)
        {
            NotifySuccess("The harvest attachments have been saved.");
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Index), new { cycleId });
    }

    private async Task<CycleWorkspaceViewModel> WorkspaceAsync(Guid? cycleId, HarvestFormViewModel form, CancellationToken cancellationToken)
    {
        CycleResponse? cycle = null;
        IReadOnlyList<HarvestResponse> harvests = [];

        if (cycleId is Guid id)
        {
            Result<CycleResponse> found = await cycleQuery.Handle(new GetCycleByIdQuery(id), cancellationToken);
            cycle = found.IsSuccess ? found.Value : null;

            Result<CyclePerformanceResponse> performance = await performanceQuery.Handle(new GetCyclePerformanceQuery(id), cancellationToken);
            harvests = performance.IsSuccess ? performance.Value.Harvests : [];
        }

        return new CycleWorkspaceViewModel
        {
            Cycle = cycle,
            CycleLabel = cycle is null ? null : LookupController.CycleLabel(cycle),
            Harvests = harvests,
            CanCreate = await support.CanAsync(MenuCode, MenuRights.Create),
            CanEdit = await support.CanAsync(MenuCode, MenuRights.Edit),
            Harvest = form,
            HarvestAttachments = await support.AttachmentsAsync([.. harvests.SelectMany(h => h.Documents).Distinct()], cancellationToken)
        };
    }
}
