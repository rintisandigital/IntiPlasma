using Application.Abstractions.Messaging;
using Application.Sales;
using Application.Users.GetCurrent;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Sales.Models;
using Web.App.Controllers;
using Web.App.Infrastructure.Auth;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Sales.Controllers;

/// <summary>
/// Sales overview: key figures of sales orders, deliveries, invoices and receivables of the branch selected in the header.
/// </summary>
[Area("Sales")]
[MenuAccess(MenuCodes.SalesOverview)]
public sealed class OverviewController(IBranchContext branchContext) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromServices] IQueryHandler<GetSalesOverviewQuery, SalesOverviewResponse> overviewQuery,
        [FromServices] DisplayFormatter formatter,
        CancellationToken cancellationToken)
    {
        CurrentUserBranch? branch = await branchContext.GetActiveBranchAsync(cancellationToken);
        Result<SalesOverviewResponse> overview = await overviewQuery.Handle(
            new GetSalesOverviewQuery(formatter.Today(), branch?.Id), cancellationToken);

        return View(new SalesOverviewViewModel(overview.IsSuccess ? overview.Value : null, branch?.Name));
    }
}
