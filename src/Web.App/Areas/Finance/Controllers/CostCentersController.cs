using Application.Abstractions.Messaging;
using Application.Finance.CostCenters;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Finance.Models;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;

namespace Web.App.Areas.Finance.Controllers;

/// <summary>
/// Cost centers (e.g. per farm area or department) used on journal lines and auto journal mappings.
/// </summary>
[Area("Finance")]
[MenuAccess(MenuCodes.FinanceCostCenters)]
public sealed class CostCentersController(
    PageSupport support,
    IQueryHandler<GetCostCentersQuery, IReadOnlyList<CostCenterResponse>> costCentersQuery) : AppController
{
    private const string MenuCode = MenuCodes.FinanceCostCenters;

    private static readonly ExportColumn<CostCenterResponse>[] Columns =
    [
        new("Code", c => c.Code, Width: 1),
        new("Name", c => c.Name, Width: 3),
        new("Active", c => c.IsActive, ExportFormat.Boolean, 0.7f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        IReadOnlyList<CostCenterResponse> costCenters = await ListAsync(search, cancellationToken);

        return View(new ListViewModel<CostCenterResponse>
        {
            Rows = new PagedList<CostCenterResponse>(costCenters, 1, Math.Max(costCenters.Count, 1), costCenters.Count),
            Search = search
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, string? search, CancellationToken cancellationToken) =>
        await support.ExportAsync(format, "Cost Centers", "cost-centers",
            string.IsNullOrWhiteSpace(search) ? [] : [$"Search: {search}"], Columns, await ListAsync(search, cancellationToken));

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public IActionResult Create() => View("Form", new CostCenterFormViewModel());

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        CostCenterFormViewModel model,
        [FromServices] ICommandHandler<CreateCostCenterCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(new CreateCostCenterCommand(model.Code, model.Name), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Cost center {model.Code} has been created.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        CostCenterResponse? costCenter = (await ListAsync(null, cancellationToken)).FirstOrDefault(c => c.Id == id);

        if (costCenter is null)
        {
            return NotFound();
        }

        return View("Form", new CostCenterFormViewModel
        {
            Id = costCenter.Id,
            Code = costCenter.Code,
            Name = costCenter.Name,
            IsActive = costCenter.IsActive,
            CanSave = await support.CanAsync(MenuCode, MenuRights.Edit)
        });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        CostCenterFormViewModel model,
        [FromServices] ICommandHandler<UpdateCostCenterCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(new UpdateCostCenterCommand(id, model.Name, model.IsActive == true), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Cost center {model.Code} has been saved.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", model);
    }

    private async Task<IReadOnlyList<CostCenterResponse>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<CostCenterResponse>> result = await costCentersQuery.Handle(new GetCostCentersQuery(), cancellationToken);

        if (result.IsFailure)
        {
            return [];
        }

        return string.IsNullOrWhiteSpace(search)
            ? result.Value
            : [.. result.Value.Where(c =>
                c.Code.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))];
    }
}
