using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Access.BranchAccessProfiles;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Admin.Models;
using Web.App.Controllers;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Areas.Admin.Controllers;

/// <summary>
/// Branch access profiles ("Akses Cabang"): all branches or a list of branches; applies to Web.Api and Web.App.
/// </summary>
[Area("Admin")]
[MenuAccess(MenuCodes.AdminBranchAccess)]
public sealed class BranchAccessController(
    AdminLookups lookups,
    IQueryHandler<GetBranchAccessProfileByIdQuery, BranchAccessProfileResponse> profileQuery) : AppController
{
    private const string MenuCode = MenuCodes.AdminBranchAccess;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? page,
        [FromServices] IQueryHandler<GetBranchAccessProfilesQuery, PagedList<BranchAccessProfileListItem>> query,
        CancellationToken cancellationToken)
    {
        Result<PagedList<BranchAccessProfileListItem>> result = await query.Handle(
            new GetBranchAccessProfilesQuery(new PageRequest(page, PageRequest.DefaultPageSize, search)), cancellationToken);

        ViewData["Search"] = search;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<BranchAccessProfileResponse> result = await profileQuery.Handle(new GetBranchAccessProfileByIdQuery(id), cancellationToken);

        return result.IsFailure ? NotFound() : View(new BranchAccessDetailsViewModel(result.Value));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("Form", new BranchAccessFormViewModel { Branches = await lookups.AllBranchesAsync(cancellationToken) });

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        BranchAccessFormViewModel model,
        [FromServices] ICommandHandler<CreateBranchAccessProfileCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateBranchAccessProfileCommand(model.Name, model.Description, model.AllBranches == true, model.BranchIds),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Branch access \"{model.Name}\" has been created.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }

            AddErrors(result.Error);
        }

        model.Branches = await lookups.AllBranchesAsync(cancellationToken);
        return View("Form", model);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        Result<BranchAccessProfileResponse> result = await profileQuery.Handle(new GetBranchAccessProfileByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        BranchAccessProfileResponse profile = result.Value;

        if (profile.IsSystem)
        {
            NotifyError("The All Branches profile is managed by the system and cannot be changed.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View("Form", new BranchAccessFormViewModel
        {
            Id = profile.Id,
            Name = profile.Name,
            Description = profile.Description,
            AllBranches = profile.AllBranches,
            BranchIds = [.. profile.Branches.Select(b => b.Id)],
            UserCount = profile.UserCount,
            Branches = await lookups.AllBranchesAsync(cancellationToken)
        });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        BranchAccessFormViewModel model,
        [FromServices] ICommandHandler<UpdateBranchAccessProfileCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateBranchAccessProfileCommand(id, model.Name, model.Description, model.AllBranches == true, model.BranchIds),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Branch access \"{model.Name}\" has been saved. Its users see the new branches immediately.");
                return RedirectToAction(nameof(Details), new { id });
            }

            AddErrors(result.Error);
        }

        model.Branches = await lookups.AllBranchesAsync(cancellationToken);
        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Delete)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteBranchAccessProfileCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new DeleteBranchAccessProfileCommand(id), cancellationToken);

        if (result.IsFailure)
        {
            NotifyError(result.Error.Description);
            return RedirectToAction(nameof(Details), new { id });
        }

        NotifySuccess("The branch access profile has been deleted.");
        return RedirectToAction(nameof(Index));
    }
}
