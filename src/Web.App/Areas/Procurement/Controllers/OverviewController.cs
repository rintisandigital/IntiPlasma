using Application.Abstractions.Messaging;
using Application.Procurement;
using Application.Users.GetCurrent;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Procurement.Models;
using Web.App.Controllers;
using Web.App.Infrastructure.Auth;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Procurement.Controllers;

/// <summary>
/// Procurement overview: key figures of purchase orders and vendor invoices of the branch selected in the header.
/// </summary>
[Area("Procurement")]
[MenuAccess(MenuCodes.ProcurementOverview)]
public sealed class OverviewController(IBranchContext branchContext) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromServices] IQueryHandler<GetProcurementOverviewQuery, ProcurementOverviewResponse> overviewQuery,
        [FromServices] DisplayFormatter formatter,
        CancellationToken cancellationToken)
    {
        CurrentUserBranch? branch = await branchContext.GetActiveBranchAsync(cancellationToken);
        Result<ProcurementOverviewResponse> overview = await overviewQuery.Handle(
            new GetProcurementOverviewQuery(formatter.Today(), branch?.Id), cancellationToken);

        return View(new ProcurementOverviewViewModel(overview.IsSuccess ? overview.Value : null, branch?.Name));
    }
}
