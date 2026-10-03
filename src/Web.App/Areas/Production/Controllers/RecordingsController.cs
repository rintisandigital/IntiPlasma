using Application.Abstractions.Messaging;
using Application.Cycles;
using Application.Cycles.GetById;
using Application.Production;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Inventory;
using Web.App.Areas.Inventory.Models;
using Web.App.Areas.Production.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Production.Controllers;

/// <summary>
/// Daily recordings entered by the admin (backup of the PPL mobile app, W-11): mortality, culling, body weight and
/// feed/OVK used from the coop warehouse. A recording is corrected by a revision (reason + previous values kept).
/// </summary>
[Area("Production")]
[MenuAccess(MenuCodes.ProductionRecordings)]
public sealed class RecordingsController(
    PageSupport support,
    InventoryOptions inventory,
    ItemOptions items,
    DisplayFormatter formatter,
    IQueryHandler<GetCycleByIdQuery, CycleResponse> cycleQuery,
    IQueryHandler<GetDailyRecordingsQuery, IReadOnlyList<DailyRecordingResponse>> recordingsQuery,
    IQueryHandler<GetDailyRecordingByIdQuery, DailyRecordingResponse> recordingQuery) : AppController
{
    private const string MenuCode = MenuCodes.ProductionRecordings;

    private static readonly ExportColumn<DailyRecordingResponse>[] Columns =
    [
        new("Date", r => r.Date, ExportFormat.Date, 1),
        new("Age (days)", r => r.AgeDays, ExportFormat.WholeNumber, 0.8f),
        new("Mortality", r => r.Mortality, ExportFormat.WholeNumber, 0.8f),
        new("Culling", r => r.Culling, ExportFormat.WholeNumber, 0.8f),
        new("Body weight (g)", r => r.AverageBodyWeightGram, ExportFormat.Number, 1),
        new("Feed (kg)", r => r.FeedKg, ExportFormat.Number, 1),
        new("Revisions", r => r.RevisionNumber, ExportFormat.WholeNumber, 0.8f),
        new("Notes", r => r.Notes, Width: 3)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(Guid? cycleId, CancellationToken cancellationToken)
    {
        CycleResponse? cycle = await CycleAsync(cycleId, cancellationToken);
        Result<IReadOnlyList<DailyRecordingResponse>>? recordings = cycle is null
            ? null
            : await recordingsQuery.Handle(new GetDailyRecordingsQuery(cycle.Id, null, null), cancellationToken);

        return View(new CycleWorkspaceViewModel
        {
            Cycle = cycle,
            CycleLabel = cycle is null ? null : LookupController.CycleLabel(cycle),
            Recordings = recordings is { IsSuccess: true } ? [.. recordings.Value.OrderByDescending(r => r.Date)] : [],
            CanCreate = await support.CanAsync(MenuCode, MenuRights.Create),
            CanEdit = await support.CanAsync(MenuCode, MenuRights.Edit)
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, Guid cycleId, CancellationToken cancellationToken)
    {
        CycleResponse? cycle = await CycleAsync(cycleId, cancellationToken);
        Result<IReadOnlyList<DailyRecordingResponse>> recordings = await recordingsQuery.Handle(new GetDailyRecordingsQuery(cycleId, null, null), cancellationToken);

        return await support.ExportAsync(format, "Daily Recordings", $"recordings-{cycle?.CoopCode}",
            [$"Cycle: {cycle?.Number} — {cycle?.CoopCode} {cycle?.CoopName}"], Columns, recordings.IsSuccess ? recordings.Value : []);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<DailyRecordingResponse> result = await recordingQuery.Handle(new GetDailyRecordingByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        DailyRecordingResponse recording = result.Value;
        CycleResponse? cycle = await CycleAsync(recording.CycleId, cancellationToken);

        return View(new RecordingDetailsViewModel(
            recording,
            cycle!,
            await support.AttachmentsAsync(recording.Documents, cancellationToken),
            CanRevise: await support.CanAsync(MenuCode, MenuRights.Edit) && cycle is { Status: "Active" or "Harvesting" }));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(Guid cycleId, CancellationToken cancellationToken)
    {
        CycleResponse? cycle = await CycleAsync(cycleId, cancellationToken);
        if (cycle is null)
        {
            return NotFound();
        }

        Result<IReadOnlyList<DailyRecordingResponse>> recordings = await recordingsQuery.Handle(new GetDailyRecordingsQuery(cycleId, null, null), cancellationToken);
        DateOnly? last = recordings.IsSuccess && recordings.Value.Count > 0 ? recordings.Value.Max(r => r.Date) : null;
        DateOnly next = last?.AddDays(1) ?? cycle.ChickInDate ?? formatter.Today();

        var model = new RecordingFormViewModel
        {
            CycleId = cycleId,
            Date = next > formatter.Today() ? formatter.Today() : next,
            Usages = []
        };

        return View("Form", await PrepareAsync(model, cycle, cancellationToken));
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        RecordingFormViewModel model,
        [FromServices] ICommandHandler<CreateDailyRecordingCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateDailyRecordingCommand(
                    null, model.CycleId!.Value, model.Date!.Value, model.Mortality!.Value, model.Culling!.Value,
                    model.AverageBodyWeightGram, model.Notes, model.ToUsages(), model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Recording of {formatter.Date(model.Date!.Value)} has been saved.");
                return RedirectToAction(nameof(Index), new { cycleId = model.CycleId });
            }

            AddErrors(result.Error);
        }

        return View("Form", await PrepareAsync(model, await CycleAsync(model.CycleId, cancellationToken), cancellationToken));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Revise(Guid id, CancellationToken cancellationToken)
    {
        Result<DailyRecordingResponse> result = await recordingQuery.Handle(new GetDailyRecordingByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        DailyRecordingResponse recording = result.Value;
        var model = new RecordingFormViewModel
        {
            Id = recording.Id,
            CycleId = recording.CycleId,
            Date = recording.Date,
            Mortality = recording.Mortality,
            Culling = recording.Culling,
            AverageBodyWeightGram = recording.AverageBodyWeightGram,
            Notes = recording.Notes,
            RevisionNumber = recording.RevisionNumber,
            Usages =
            [
                .. (recording.Usages ?? []).Select(u => new StockLineInput
                {
                    ItemId = u.ItemId,
                    UomId = null,
                    ItemLabel = $"{u.ItemCode} — {u.ItemName}",
                    Quantity = u.BaseQuantity
                })
            ],
            Documents = [.. recording.Documents]
        };

        return View("Form", await PrepareAsync(model, await CycleAsync(recording.CycleId, cancellationToken), cancellationToken));
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Revise(
        RecordingFormViewModel model,
        [FromServices] ICommandHandler<ReviseDailyRecordingCommand> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), "Enter the reason of the revision.");
        }

        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new ReviseDailyRecordingCommand(
                    id, model.Reason!, model.Mortality!.Value, model.Culling!.Value, model.AverageBodyWeightGram, model.Notes,
                    model.ToUsages(), model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess("The recording has been revised; the previous values are kept in its history.");
                return RedirectToAction(nameof(Details), new { id });
            }

            AddErrors(result.Error);
        }

        return View("Form", await PrepareAsync(model, await CycleAsync(model.CycleId, cancellationToken), cancellationToken));
    }

    private async Task<CycleResponse?> CycleAsync(Guid? cycleId, CancellationToken cancellationToken)
    {
        if (cycleId is not Guid id)
        {
            return null;
        }

        Result<CycleResponse> cycle = await cycleQuery.Handle(new GetCycleByIdQuery(id), cancellationToken);
        return cycle.IsSuccess ? cycle.Value : null;
    }

    private async Task<RecordingFormViewModel> PrepareAsync(RecordingFormViewModel model, CycleResponse? cycle, CancellationToken cancellationToken)
    {
        model.Cycle = cycle;
        model.CoopWarehouseId = cycle is null ? null : await inventory.CoopWarehouseIdAsync(cycle.CoopId, cancellationToken);
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);

        foreach (StockLineInput line in model.Usages)
        {
            line.ItemLabel ??= await items.LabelAsync(line.ItemId, cancellationToken);
            line.Units = await items.UnitsAsync(line.ItemId, line.UomId, cancellationToken);
            line.UomId ??= line.Units.Count > 0 ? Guid.Parse(line.Units[0].Value) : null;
        }

        return model;
    }
}
