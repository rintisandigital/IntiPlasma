using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Branches;
using Application.Branches.Create;
using Application.Branches.Get;
using Application.Branches.GetById;
using Application.Branches.Update;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Admin.Models;
using Web.App.Controllers;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Areas.Admin.Controllers;

/// <summary>
/// Branch master data. Deactivating a branch removes it from every user's effective branches.
/// </summary>
[Area("Admin")]
[MenuAccess(MenuCodes.AdminBranches)]
public sealed class BranchesController : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? page,
        [FromServices] IQueryHandler<GetBranchesQuery, PagedList<BranchResponse>> query,
        CancellationToken cancellationToken)
    {
        Result<PagedList<BranchResponse>> result = await query.Handle(
            new GetBranchesQuery(new PageRequest(page, PageRequest.DefaultPageSize, search)), cancellationToken);

        ViewData["Search"] = search;
        return View(result.Value);
    }

    [HttpGet]
    [MenuAccess(MenuCodes.AdminBranches, MenuRights.Create)]
    public IActionResult Create() => View("Form", new BranchFormViewModel { IsActive = true });

    [HttpPost]
    [MenuAccess(MenuCodes.AdminBranches, MenuRights.Create)]
    public async Task<IActionResult> Create(
        BranchFormViewModel model,
        [FromServices] ICommandHandler<CreateBranchCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        Result<Guid> result = await handler.Handle(
            new CreateBranchCommand(model.Code, model.Name, model.Address, model.Phone), cancellationToken);

        if (result.IsFailure)
        {
            AddErrors(result.Error);
            return View("Form", model);
        }

        NotifySuccess($"Branch {model.Code} has been created.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [MenuAccess(MenuCodes.AdminBranches, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        Guid id,
        [FromServices] IQueryHandler<GetBranchByIdQuery, BranchResponse> query,
        CancellationToken cancellationToken)
    {
        Result<BranchResponse> result = await query.Handle(new GetBranchByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        BranchResponse branch = result.Value;

        return View("Form", new BranchFormViewModel
        {
            Id = branch.Id,
            Code = branch.Code,
            Name = branch.Name,
            Address = branch.Address,
            Phone = branch.Phone,
            IsActive = branch.IsActive
        });
    }

    [HttpPost]
    [MenuAccess(MenuCodes.AdminBranches, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        BranchFormViewModel model,
        [FromServices] ICommandHandler<UpdateBranchCommand> handler,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.Id is not Guid id)
        {
            return View("Form", model);
        }

        Result result = await handler.Handle(
            new UpdateBranchCommand(id, model.Name, model.Address, model.Phone, model.IsActive == true), cancellationToken);

        if (result.IsFailure)
        {
            AddErrors(result.Error);
            return View("Form", model);
        }

        NotifySuccess($"Branch {model.Code} has been saved.");
        return RedirectToAction(nameof(Index));
    }
}
