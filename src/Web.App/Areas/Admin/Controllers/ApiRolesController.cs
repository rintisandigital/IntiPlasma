using Application.Abstractions.Messaging;
using Application.Roles.Create;
using Application.Roles.Update;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Admin.Models;
using Web.App.Controllers;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Areas.Admin.Controllers;

/// <summary>
/// Roles and their API permissions (Web.Api / mobile). Web.App itself is authorized by menu access.
/// </summary>
[Area("Admin")]
[MenuAccess(MenuCodes.AdminApiRoles)]
public sealed class ApiRolesController(AdminLookups lookups) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await lookups.RolesAsync(cancellationToken));

    [HttpGet]
    [MenuAccess(MenuCodes.AdminApiRoles, MenuRights.Create)]
    public IActionResult Create() => View("Form", new ApiRoleFormViewModel());

    [HttpPost]
    [MenuAccess(MenuCodes.AdminApiRoles, MenuRights.Create)]
    public async Task<IActionResult> Create(
        ApiRoleFormViewModel model,
        [FromServices] ICommandHandler<CreateRoleCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        Result<Guid> result = await handler.Handle(
            new CreateRoleCommand(model.Name, model.Description, model.Permissions), cancellationToken);

        if (result.IsFailure)
        {
            AddErrors(result.Error);
            return View("Form", model);
        }

        NotifySuccess($"Role \"{model.Name}\" has been created.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        Application.Roles.Get.RoleResponse? role = (await lookups.RolesAsync(cancellationToken)).FirstOrDefault(r => r.Id == id);

        if (role is null)
        {
            return NotFound();
        }

        return View("Form", new ApiRoleFormViewModel
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsSystem = role.IsSystem,
            Permissions = [.. role.Permissions]
        });
    }

    [HttpPost]
    [MenuAccess(MenuCodes.AdminApiRoles, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        ApiRoleFormViewModel model,
        [FromServices] ICommandHandler<UpdateRoleCommand> handler,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.Id is not Guid id)
        {
            return View("Form", model);
        }

        Result result = await handler.Handle(
            new UpdateRoleCommand(id, model.Name, model.Description, model.Permissions), cancellationToken);

        if (result.IsFailure)
        {
            AddErrors(result.Error);
            return View("Form", model);
        }

        NotifySuccess($"Role \"{model.Name}\" has been saved.");
        return RedirectToAction(nameof(Index));
    }
}
