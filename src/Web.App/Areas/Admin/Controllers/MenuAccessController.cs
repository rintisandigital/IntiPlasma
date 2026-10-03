using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Access.MenuAccessProfiles;
using Application.Access.Menus;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Admin.Models;
using Web.App.Controllers;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Areas.Admin.Controllers;

/// <summary>
/// Menu access profiles ("Akses Menu"): a matrix of menus × View/Create/Edit/Delete/Export.
/// </summary>
[Area("Admin")]
[MenuAccess(MenuCodes.AdminMenuAccess)]
public sealed class MenuAccessController(
    IQueryHandler<GetMenusQuery, IReadOnlyList<MenuResponse>> menusQuery,
    IQueryHandler<GetMenuAccessProfileByIdQuery, MenuAccessProfileResponse> profileQuery) : AppController
{
    private const string MenuCode = MenuCodes.AdminMenuAccess;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? page,
        [FromServices] IQueryHandler<GetMenuAccessProfilesQuery, PagedList<MenuAccessProfileListItem>> query,
        CancellationToken cancellationToken)
    {
        Result<PagedList<MenuAccessProfileListItem>> result = await query.Handle(
            new GetMenuAccessProfilesQuery(new PageRequest(page, PageRequest.DefaultPageSize, search)), cancellationToken);

        ViewData["Search"] = search;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        MenuAccessFormViewModel? model = await LoadAsync(id, cancellationToken);

        return model is null ? NotFound() : View(model);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new MenuAccessFormViewModel();
        model.ApplyCatalog(await MenusAsync(cancellationToken));

        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        MenuAccessFormViewModel model,
        [FromServices] ICommandHandler<CreateMenuAccessProfileCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateMenuAccessProfileCommand(model.Name, model.Description, model.ToGrants()), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Menu access \"{model.Name}\" has been created.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }

            AddErrors(result.Error);
        }

        model.ApplyCatalog(await MenusAsync(cancellationToken));
        return View("Form", model);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        MenuAccessFormViewModel? model = await LoadAsync(id, cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        if (model.IsSystem)
        {
            NotifyError("The Full Access profile is managed by the system and cannot be changed.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        MenuAccessFormViewModel model,
        [FromServices] ICommandHandler<UpdateMenuAccessProfileCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateMenuAccessProfileCommand(id, model.Name, model.Description, model.ToGrants()), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Menu access \"{model.Name}\" has been saved. Its users get the new rights immediately.");
                return RedirectToAction(nameof(Details), new { id });
            }

            AddErrors(result.Error);
        }

        model.ApplyCatalog(await MenusAsync(cancellationToken));
        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Duplicate(
        Guid id,
        DuplicateInput input,
        [FromServices] ICommandHandler<DuplicateMenuAccessProfileCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            NotifyError("Enter a name for the copy.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result<Guid> result = await handler.Handle(new DuplicateMenuAccessProfileCommand(id, input.Name), cancellationToken);

        if (result.IsFailure)
        {
            NotifyError(result.Error.Description);
            return RedirectToAction(nameof(Details), new { id });
        }

        NotifySuccess($"Menu access \"{input.Name}\" has been created as a copy.");
        return RedirectToAction(nameof(Edit), new { id = result.Value });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Delete)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteMenuAccessProfileCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new DeleteMenuAccessProfileCommand(id), cancellationToken);

        if (result.IsFailure)
        {
            NotifyError(result.Error.Description);
            return RedirectToAction(nameof(Details), new { id });
        }

        NotifySuccess("The menu access profile has been deleted.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<MenuAccessFormViewModel?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        Result<MenuAccessProfileResponse> result = await profileQuery.Handle(new GetMenuAccessProfileByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return null;
        }

        MenuAccessProfileResponse profile = result.Value;

        var model = new MenuAccessFormViewModel
        {
            Id = profile.Id,
            Name = profile.Name,
            Description = profile.Description,
            IsSystem = profile.IsSystem,
            UserCount = profile.UserCount
        };

        model.ApplyCatalog(await MenusAsync(cancellationToken), profile.Items.ToDictionary(i => i.MenuId, i => i.Rights));

        return model;
    }

    private async Task<IReadOnlyList<MenuResponse>> MenusAsync(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<MenuResponse>> result = await menusQuery.Handle(new GetMenusQuery(), cancellationToken);

        return result.IsSuccess ? result.Value : [];
    }
}
