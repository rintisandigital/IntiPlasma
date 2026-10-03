using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Users.AssignRoles;
using Application.Users.GetById;
using Application.Users.Manage;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Admin.Models;
using Web.App.Controllers;
using Web.App.Infrastructure.Auth;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Areas.Admin.Controllers;

/// <summary>
/// User administration (PLAN-WEBAPP §11.7): create with initial password and access, change access,
/// activate/deactivate, reset password and delete (with backup, W-22).
/// </summary>
[Area("Admin")]
[MenuAccess(MenuCodes.AdminUsers)]
public sealed class UsersController(AdminLookups lookups) : AppController
{
    private const string MenuCode = MenuCodes.AdminUsers;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        Guid? menuAccessProfileId,
        Guid? branchAccessProfileId,
        int? page,
        [FromServices] IQueryHandler<GetUsersQuery, PagedList<UserListItem>> query,
        CancellationToken cancellationToken)
    {
        bool? isActive = status switch
        {
            "active" => true,
            "inactive" => false,
            _ => null
        };

        Result<PagedList<UserListItem>> result = await query.Handle(
            new GetUsersQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), isActive, menuAccessProfileId, branchAccessProfileId),
            cancellationToken);

        return View(new UserIndexViewModel
        {
            Users = result.Value,
            Search = search,
            Status = status,
            MenuAccessProfileId = menuAccessProfileId,
            BranchAccessProfileId = branchAccessProfileId,
            MenuProfiles = await lookups.MenuProfilesAsync(menuAccessProfileId, cancellationToken),
            BranchProfiles = await lookups.BranchProfilesAsync(branchAccessProfileId, cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        [FromServices] IQueryHandler<GetUserByIdQuery, UserResponse> query,
        CancellationToken cancellationToken)
    {
        Result<UserResponse> result = await query.Handle(new GetUserByIdQuery(id), cancellationToken);

        return result.IsFailure
            ? NotFound()
            : View(new UserDetailsViewModel(result.Value, result.Value.Id == User.GetUserId()));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new UserCreateViewModel();
        model.Options = await OptionsAsync(model.MenuAccessProfileId, model.BranchAccessProfileId, model.DefaultBranchId, cancellationToken);

        return View(model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        UserCreateViewModel model,
        [FromServices] ICommandHandler<CreateUserCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateUserCommand(
                    model.Email,
                    model.FirstName,
                    model.LastName,
                    model.Password,
                    model.MenuAccessProfileId,
                    model.BranchAccessProfileId,
                    model.DefaultBranchId,
                    model.RoleIds),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"User {model.Email} has been created.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }

            AddErrors(result.Error);
        }

        model.Options = await OptionsAsync(model.MenuAccessProfileId, model.BranchAccessProfileId, model.DefaultBranchId, cancellationToken);
        return View(model);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        Guid id,
        [FromServices] IQueryHandler<GetUserByIdQuery, UserResponse> query,
        CancellationToken cancellationToken)
    {
        Result<UserResponse> result = await query.Handle(new GetUserByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        return View(new UserEditViewModel
        {
            Id = result.Value.Id,
            Email = result.Value.Email,
            FirstName = result.Value.FirstName,
            LastName = result.Value.LastName
        });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        UserEditViewModel model,
        [FromServices] ICommandHandler<UpdateUserCommand> handler,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        Result result = await handler.Handle(new UpdateUserCommand(model.Id!.Value, model.FirstName, model.LastName), cancellationToken);

        if (result.IsFailure)
        {
            AddErrors(result.Error);
            return View(model);
        }

        NotifySuccess("The user has been updated.");
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Access(
        Guid id,
        [FromServices] IQueryHandler<GetUserByIdQuery, UserResponse> query,
        CancellationToken cancellationToken)
    {
        Result<UserResponse> result = await query.Handle(new GetUserByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        UserResponse user = result.Value;

        return View(new UserAccessViewModel
        {
            Id = user.Id,
            DisplayName = $"{user.FirstName} {user.LastName} ({user.Email})",
            MenuAccessProfileId = user.MenuAccessProfileId,
            BranchAccessProfileId = user.BranchAccessProfileId,
            DefaultBranchId = user.DefaultBranchId,
            RoleIds = [.. user.RoleIds],
            Options = await OptionsAsync(user.MenuAccessProfileId, user.BranchAccessProfileId, user.DefaultBranchId, cancellationToken)
        });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Access(
        UserAccessViewModel model,
        [FromServices] ICommandHandler<SetUserAccessCommand> accessHandler,
        [FromServices] ICommandHandler<AssignUserRolesCommand> rolesHandler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result result = await accessHandler.Handle(
                new SetUserAccessCommand(model.Id!.Value, model.MenuAccessProfileId, model.BranchAccessProfileId, model.DefaultBranchId),
                cancellationToken);

            if (result.IsSuccess)
            {
                result = await rolesHandler.Handle(new AssignUserRolesCommand(model.Id!.Value, model.RoleIds), cancellationToken);
            }

            if (result.IsSuccess)
            {
                NotifySuccess("The user's access has been updated.");
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }

            AddErrors(result.Error);
        }

        model.Options = await OptionsAsync(model.MenuAccessProfileId, model.BranchAccessProfileId, model.DefaultBranchId, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Deactivate(
        Guid id,
        [FromServices] ICommandHandler<DeactivateUserCommand> handler,
        CancellationToken cancellationToken) =>
        DetailsAfter(id, await handler.Handle(new DeactivateUserCommand(id), cancellationToken), "The user has been deactivated.");

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Activate(
        Guid id,
        [FromServices] ICommandHandler<ActivateUserCommand> handler,
        CancellationToken cancellationToken) =>
        DetailsAfter(id, await handler.Handle(new ActivateUserCommand(id), cancellationToken), "The user has been activated.");

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        ResetPasswordInput input,
        [FromServices] ICommandHandler<ResetUserPasswordCommand> handler,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            NotifyError("The new password must have at least 8 characters.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return DetailsAfter(
            id,
            await handler.Handle(new ResetUserPasswordCommand(id, input.NewPassword), cancellationToken),
            "The password has been reset. Give the new password to the user.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Delete)]
    public async Task<IActionResult> Delete(
        Guid id,
        DeleteUserInput input,
        [FromServices] ICommandHandler<DeleteUserCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new DeleteUserCommand(id, input.Reason), cancellationToken);

        if (result.IsFailure)
        {
            NotifyError(result.Error.Description);
            return RedirectToAction(nameof(Details), new { id });
        }

        NotifySuccess("The user has been deleted (a backup copy was kept).");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Active branches of a branch access profile, for the default branch dropdown (AJAX).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ProfileBranches(Guid? profileId, CancellationToken cancellationToken) =>
        Json((await lookups.ProfileBranchesAsync(profileId, null, cancellationToken))
            .Select(b => new { id = b.Value, name = b.Text }));

    private RedirectToActionResult DetailsAfter(Guid id, Result result, string successMessage)
    {
        if (result.IsSuccess)
        {
            NotifySuccess(successMessage);
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<UserAccessOptions> OptionsAsync(
        Guid? menuProfileId,
        Guid? branchProfileId,
        Guid? defaultBranchId,
        CancellationToken cancellationToken) => new()
        {
            MenuProfiles = await lookups.MenuProfilesAsync(menuProfileId, cancellationToken),
            BranchProfiles = await lookups.BranchProfilesAsync(branchProfileId, cancellationToken),
            DefaultBranches = await lookups.ProfileBranchesAsync(branchProfileId, defaultBranchId, cancellationToken),
            Roles = await lookups.RolesAsync(cancellationToken)
        };
}
