using Application.Abstractions.Messaging;
using Application.Monitoring;
using Application.Users.GetCurrent;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Infrastructure.Auth;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Controllers;

/// <param name="Summary">Null when the figures could not be loaded.</param>
/// <param name="BranchName">The branch the figures are for (null = all accessible branches).</param>
public sealed record DashboardViewModel(DashboardSummaryResponse? Summary, string? BranchName);

/// <summary>
/// The mainboard (header + sidebar + content iframe) and the dashboard shown in the iframe.
/// </summary>
[AuthenticatedOnly]
public sealed class MainController(IBranchContext branchContext) : AppController
{
    [HttpGet]
    public IActionResult Index() => View();

    /// <summary>
    /// Key figures of the branch selected in the header (PLAN-WEBAPP §21.2).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Dashboard(
        [FromServices] IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryResponse> summaryQuery,
        [FromServices] DisplayFormatter formatter,
        CancellationToken cancellationToken)
    {
        CurrentUserBranch? branch = await branchContext.GetActiveBranchAsync(cancellationToken);
        Result<DashboardSummaryResponse> summary = await summaryQuery.Handle(
            new GetDashboardSummaryQuery(formatter.Today(), branch?.Id), cancellationToken);

        return View(new DashboardViewModel(summary.IsSuccess ? summary.Value : null, branch?.Name));
    }

    /// <summary>
    /// "Stay signed in" of the session warning: any authenticated request renews the sliding cookie; returns the
    /// new expiry (Unix milliseconds).
    /// </summary>
    [HttpPost]
    public IActionResult KeepAlive() =>
        Json(new { expires = SessionExpiry.Get(HttpContext)?.ToUnixTimeMilliseconds() });

    /// <summary>
    /// Selects the branch used as the default filter (AJAX from the header; empty = all branches).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SwitchBranch(Guid? branchId, CancellationToken cancellationToken) =>
        await branchContext.SelectAsync(branchId, cancellationToken) ? NoContent() : Forbid();
}
