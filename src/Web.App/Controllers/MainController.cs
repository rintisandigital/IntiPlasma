using Microsoft.AspNetCore.Mvc;
using Web.App.Infrastructure.Auth;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Controllers;

/// <summary>
/// The mainboard (header + sidebar + content iframe) and the dashboard shown in the iframe.
/// </summary>
[AuthenticatedOnly]
public sealed class MainController(IBranchContext branchContext) : AppController
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public IActionResult Dashboard() => View();

    /// <summary>
    /// Selects the branch used as the default filter (AJAX from the header; empty = all branches).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SwitchBranch(Guid? branchId, CancellationToken cancellationToken) =>
        await branchContext.SelectAsync(branchId, cancellationToken) ? NoContent() : Forbid();
}
