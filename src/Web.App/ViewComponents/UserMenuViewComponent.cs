using Microsoft.AspNetCore.Mvc;
using Web.App.Infrastructure.Auth;

namespace Web.App.ViewComponents;

public sealed record UserMenuModel(string DisplayName, string Email, string Initials);

/// <summary>
/// Header avatar dropdown: change password (in the content iframe) and sign out (whole window).
/// </summary>
public sealed class UserMenuViewComponent : ViewComponent
{
    public IViewComponentResult Invoke() =>
        View(new UserMenuModel(UserClaimsPrincipal.GetDisplayName(), UserClaimsPrincipal.GetEmail(), UserClaimsPrincipal.GetInitials()));
}
