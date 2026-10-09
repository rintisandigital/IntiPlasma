using Application.Abstractions.Messaging;
using Application.WeightRanges;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;

namespace Web.App.Areas.MasterData.Controllers;

/// <summary>
/// Rentang bobot for the daily live bird stock the PPL report on the mobile app (PLAN-MOBILE M-23, M-46). Global for
/// all branches; active ranges may not overlap; the bounds lock once a stock entry uses the range.
/// </summary>
[Area("MasterData")]
[MenuAccess(MenuCodes.MasterWeightRanges)]
public sealed class WeightRangesController(
    PageSupport support,
    IQueryHandler<GetWeightRangesQuery, IReadOnlyList<WeightRangeResponse>> rangesQuery) : AppController
{
    private const string MenuCode = MenuCodes.MasterWeightRanges;

    private static readonly ExportColumn<WeightRangeResponse>[] Columns =
    [
        new("Code", r => r.Code, Width: 1),
        new("Name", r => r.Name, Width: 2),
        new("From (kg)", r => r.MinWeightKg, ExportFormat.Number, 1),
        new("Below (kg)", r => r.MaxWeightKg, ExportFormat.Number, 1),
        new("Order", r => r.SortOrder, ExportFormat.WholeNumber, 0.6f),
        new("Active", r => r.IsActive, ExportFormat.Boolean, 0.6f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        IReadOnlyList<WeightRangeResponse> ranges = await ListAsync(search, cancellationToken);

        return View(new ListViewModel<WeightRangeResponse>
        {
            Rows = new PagedList<WeightRangeResponse>(ranges, 1, Math.Max(ranges.Count, 1), ranges.Count),
            Search = search
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, string? search, CancellationToken cancellationToken) =>
        await support.ExportAsync(format, "Weight Ranges", "weight-ranges",
            string.IsNullOrWhiteSpace(search) ? [] : [$"Search: {search}"], Columns, await ListAsync(search, cancellationToken));

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public IActionResult Create() => View("Form", new WeightRangeFormViewModel());

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        WeightRangeFormViewModel model,
        [FromServices] ICommandHandler<CreateWeightRangeCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateWeightRangeCommand(model.Code, model.Name, model.MinWeightKg, model.MaxWeightKg, model.SortOrder),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Weight range {model.Code} has been created.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        WeightRangeResponse? range = (await ListAsync(null, cancellationToken)).FirstOrDefault(r => r.Id == id);

        if (range is null)
        {
            return NotFound();
        }

        return View("Form", new WeightRangeFormViewModel
        {
            Id = range.Id,
            Code = range.Code,
            Name = range.Name,
            MinWeightKg = range.MinWeightKg,
            MaxWeightKg = range.MaxWeightKg,
            SortOrder = range.SortOrder,
            IsActive = range.IsActive,
            CanSave = await support.CanAsync(MenuCode, MenuRights.Edit)
        });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        WeightRangeFormViewModel model,
        [FromServices] ICommandHandler<UpdateWeightRangeCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateWeightRangeCommand(id, model.Name, model.MinWeightKg, model.MaxWeightKg, model.SortOrder, model.IsActive == true),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Weight range {model.Code} has been saved.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", model);
    }

    private async Task<IReadOnlyList<WeightRangeResponse>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<WeightRangeResponse>> result = await rangesQuery.Handle(new GetWeightRangesQuery(), cancellationToken);
        IReadOnlyList<WeightRangeResponse> ranges = result.IsSuccess ? result.Value : [];

        return string.IsNullOrWhiteSpace(search)
            ? ranges
            : [.. ranges.Where(r => r.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                   r.Name.Contains(search, StringComparison.OrdinalIgnoreCase))];
    }
}
