using Application.Users.GetCurrent;
using Microsoft.AspNetCore.Mvc;
using Web.App.Infrastructure.Auth;

namespace Web.App.ViewComponents;

/// <param name="PhotoId">Profile photo (attachment id), null to show the initials.</param>
public sealed record UserMenuModel(string DisplayName, string Email, string Initials, Guid? PhotoId);

/// <summary>
/// Header avatar dropdown: profile (in the content iframe) and sign out (whole window).
/// </summary>
public sealed class UserMenuViewComponent(IBranchContext branchContext) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        // Loaded once per request and shared with the branch switcher.
        CurrentUserResponse? user = await branchContext.GetUserAsync(HttpContext.RequestAborted);

        return View(new UserMenuModel(
            UserClaimsPrincipal.GetDisplayName(),
            UserClaimsPrincipal.GetEmail(),
            UserClaimsPrincipal.GetInitials(),
            user?.PhotoId));
    }
}
