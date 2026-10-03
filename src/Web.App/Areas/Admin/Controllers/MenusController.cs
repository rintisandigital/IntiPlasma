using Application.Abstractions.Messaging;
using Application.Access.Menus;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Admin.Models;
using Web.App.Controllers;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Areas.Admin.Controllers;

/// <summary>
/// Menu display settings: name, icon, order and active flag (structure and rights come from the code catalog).
/// </summary>
[Area("Admin")]
[MenuAccess(MenuCodes.AdminMenus)]
public sealed class MenusController(IQueryHandler<GetMenusQuery, IReadOnlyList<MenuResponse>> menusQuery) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await MenusAsync(cancellationToken));

    [HttpGet]
    [MenuAccess(MenuCodes.AdminMenus, MenuRights.Edit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        MenuResponse? menu = (await MenusAsync(cancellationToken)).FirstOrDefault(m => m.Id == id);

        if (menu is null)
        {
            return NotFound();
        }

        return View(new MenuEditViewModel
        {
            Id = menu.Id,
            Code = menu.Code,
            DefaultName = menu.DefaultName,
            Name = menu.Name,
            Icon = menu.Icon,
            SortOrder = menu.SortOrder,
            IsActive = menu.IsActive,
            IsGroup = menu.ParentCode is null
        });
    }

    [HttpPost]
    [MenuAccess(MenuCodes.AdminMenus, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        MenuEditViewModel model,
        [FromServices] ICommandHandler<UpdateMenuCommand> handler,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        Result result = await handler.Handle(
            new UpdateMenuCommand(model.Id!.Value, model.Name, model.Icon, model.SortOrder ?? 0, model.IsActive == true),
            cancellationToken);

        if (result.IsFailure)
        {
            AddErrors(result.Error);
            return View(model);
        }

        NotifySuccess("The menu has been saved. Reload the page to see the sidebar change.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<IReadOnlyList<MenuResponse>> MenusAsync(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<MenuResponse>> result = await menusQuery.Handle(new GetMenusQuery(), cancellationToken);

        return result.IsSuccess ? result.Value : [];
    }
}
