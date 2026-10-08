using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Warehouses;
using Application.Warehouses.Create;
using Application.Warehouses.Get;
using Application.Warehouses.GetById;
using Application.Warehouses.Update;
using Domain.Access;
using Domain.MasterData.Warehouses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;

namespace Web.App.Areas.MasterData.Controllers;

/// <summary>
/// Central warehouses (gudang induk) per branch; coop warehouses (GK-…) are created with their coop.
/// </summary>
[Area("MasterData")]
[MenuAccess(MenuCodes.MasterWarehouses)]
public sealed class WarehousesController(
    PageSupport support,
    IQueryHandler<GetWarehousesQuery, PagedList<WarehouseResponse>> warehousesQuery) : AppController
{
    private const string MenuCode = MenuCodes.MasterWarehouses;

    private static readonly ExportColumn<WarehouseResponse>[] Columns =
    [
        new("Code", w => w.Code, Width: 1.2f),
        new("Name", w => w.Name, Width: 3),
        new("Branch", w => w.BranchCode, Width: 1),
        new("Type", w => EnumOptions.Label(w.Type), Width: 1),
        new("Farm", w => w.CoopCode, Width: 1),
        new("Address", w => w.Address, Width: 3),
        new("Active", w => w.IsActive, ExportFormat.Boolean, 0.8f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? branch, WarehouseType? type, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<WarehouseResponse>> result = await warehousesQuery.Handle(
            new GetWarehousesQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, type),
            cancellationToken);

        return View(new ListViewModel<WarehouseResponse>
        {
            Rows = result.Value,
            Search = search,
            BranchOptions = branchFilter.Options,
            Filters = new Dictionary<string, string?> { ["type"] = type?.ToString() },
            FilterOptions = new Dictionary<string, IReadOnlyList<SelectListItem>> { ["type"] = EnumOptions.For(type) }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, string? search, string? branch, WarehouseType? type, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        List<string> filters = [branchFilter.Description];
        if (type is not null)
        {
            filters.Add($"Type: {type}");
        }

        return await support.ExportAsync(format, "Warehouses", "warehouses", filters, Columns,
            (paging, ct) => warehousesQuery.Handle(new GetWarehousesQuery(paging, branchFilter.BranchId, type), ct), search, cancellationToken);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create() =>
        View("Form", new WarehouseFormViewModel { Branches = await support.BranchOptionsAsync(null) });

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        WarehouseFormViewModel model,
        [FromServices] ICommandHandler<CreateWarehouseCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateWarehouseCommand(model.Code, model.Name, model.BranchId!.Value, model.Address), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Warehouse {model.Code} has been created.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        model.Branches = await support.BranchOptionsAsync(model.BranchId);
        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        Guid id,
        [FromServices] IQueryHandler<GetWarehouseByIdQuery, WarehouseResponse> query,
        CancellationToken cancellationToken)
    {
        Result<WarehouseResponse> result = await query.Handle(new GetWarehouseByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        WarehouseResponse warehouse = result.Value;

        return View("Form", new WarehouseFormViewModel
        {
            Id = warehouse.Id,
            Code = warehouse.Code,
            Name = warehouse.Name,
            BranchId = warehouse.BranchId,
            Address = warehouse.Address,
            IsActive = warehouse.IsActive,
            Type = warehouse.Type,
            CoopCode = warehouse.CoopCode,
            Branches = await support.BranchOptionsAsync(warehouse.BranchId),
            CanSave = await support.CanAsync(MenuCode, MenuRights.Edit)
        });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        WarehouseFormViewModel model,
        [FromServices] ICommandHandler<UpdateWarehouseCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateWarehouseCommand(id, model.Name, model.Address, model.IsActive == true), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Warehouse {model.Code} has been saved.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        model.Branches = await support.BranchOptionsAsync(model.BranchId);
        return View("Form", model);
    }
}
