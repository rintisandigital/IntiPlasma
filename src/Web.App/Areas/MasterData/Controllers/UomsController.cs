using Application.Abstractions.Messaging;
using Application.Uoms.Create;
using Application.Uoms.Get;
using Application.Uoms.Update;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;

namespace Web.App.Areas.MasterData.Controllers;

[Area("MasterData")]
[MenuAccess(MenuCodes.MasterUoms)]
public sealed class UomsController(
    PageSupport support,
    IQueryHandler<GetUomsQuery, IReadOnlyList<UomResponse>> uomsQuery) : AppController
{
    private const string MenuCode = MenuCodes.MasterUoms;

    private static readonly ExportColumn<UomResponse>[] Columns =
    [
        new("Code", u => u.Code, Width: 1),
        new("Name", u => u.Name, Width: 3)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        IReadOnlyList<UomResponse> uoms = await ListAsync(search, cancellationToken);

        return View(new ListViewModel<UomResponse>
        {
            Rows = new PagedList<UomResponse>(uoms, 1, Math.Max(uoms.Count, 1), uoms.Count),
            Search = search
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, string? search, CancellationToken cancellationToken) =>
        await support.ExportAsync(format, "Units of Measure", "uoms", Filters(search), Columns, await ListAsync(search, cancellationToken));

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public IActionResult Create() => View("Form", new UomFormViewModel());

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        UomFormViewModel model,
        [FromServices] ICommandHandler<CreateUomCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(new CreateUomCommand(model.Code, model.Name), cancellationToken);
            if (result.IsSuccess)
            {
                NotifySuccess($"Unit {model.Code} has been created.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        UomResponse? uom = (await ListAsync(null, cancellationToken)).FirstOrDefault(u => u.Id == id);

        if (uom is null)
        {
            return NotFound();
        }

        return View("Form", new UomFormViewModel
        {
            Id = uom.Id,
            Code = uom.Code,
            Name = uom.Name,
            CanSave = await support.CanAsync(MenuCode, MenuRights.Edit)
        });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        UomFormViewModel model,
        [FromServices] ICommandHandler<UpdateUomCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(new UpdateUomCommand(id, model.Name), cancellationToken);
            if (result.IsSuccess)
            {
                NotifySuccess($"Unit {model.Code} has been saved.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", model);
    }

    private async Task<IReadOnlyList<UomResponse>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<UomResponse>> result = await uomsQuery.Handle(new GetUomsQuery(), cancellationToken);
        IReadOnlyList<UomResponse> uoms = result.IsSuccess ? result.Value : [];

        return string.IsNullOrWhiteSpace(search)
            ? uoms
            : [.. uoms.Where(u => u.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                 u.Name.Contains(search, StringComparison.OrdinalIgnoreCase))];
    }

    private static string[] Filters(string? search) => string.IsNullOrWhiteSpace(search) ? [] : [$"Search: {search}"];
}
