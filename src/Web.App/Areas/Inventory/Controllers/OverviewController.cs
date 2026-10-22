using Application.Abstractions.Messaging;
using Application.Inventory;
using Application.Users.GetCurrent;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Inventory.Models;
using Web.App.Controllers;
using Web.App.Infrastructure.Auth;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Inventory.Controllers;

/// <summary>
/// Inventory overview: key figures of stock and stock documents of the branch selected in the header.
/// </summary>
[Area("Inventory")]
[MenuAccess(MenuCodes.InventoryOverview)]
public sealed class OverviewController(IBranchContext branchContext) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromServices] IQueryHandler<GetInventoryOverviewQuery, InventoryOverviewResponse> overviewQuery,
        [FromServices] DisplayFormatter formatter,
        CancellationToken cancellationToken)
    {
        CurrentUserBranch? branch = await branchContext.GetActiveBranchAsync(cancellationToken);
        Result<InventoryOverviewResponse> overview = await overviewQuery.Handle(
            new GetInventoryOverviewQuery(formatter.Today(), branch?.Id), cancellationToken);

        return View(new InventoryOverviewViewModel(overview.IsSuccess ? overview.Value : null, branch?.Name));
    }
}
