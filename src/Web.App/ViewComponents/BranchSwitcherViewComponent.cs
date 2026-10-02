using Application.Users.GetCurrent;
using Microsoft.AspNetCore.Mvc;
using Web.App.Infrastructure.Auth;

namespace Web.App.ViewComponents;

public sealed record BranchSwitcherModel(
    IReadOnlyList<CurrentUserBranch> Branches,
    bool AllBranchesAllowed,
    CurrentUserBranch? Active);

/// <summary>
/// Header dropdown to pick the branch used as the default filter of every page.
/// </summary>
public sealed class BranchSwitcherViewComponent(IBranchContext branchContext) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        CurrentUserResponse? user = await branchContext.GetUserAsync(HttpContext.RequestAborted);
        CurrentUserBranch? active = await branchContext.GetActiveBranchAsync(HttpContext.RequestAborted);

        return View(new BranchSwitcherModel(user?.Branches ?? [], user?.AllBranches ?? false, active));
    }
}
