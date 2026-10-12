using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Approvals;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Infrastructure.Authorization;

namespace Web.App.ViewComponents;

/// <param name="WaitingForMe">Pending requests the user may decide now.</param>
/// <param name="Stuck">Pending requests nobody can decide (null when the user may not see Approval Requests).</param>
public sealed record ApprovalBadgeModel(int WaitingForMe, int? Stuck);

/// <summary>
/// Approval counts (PLAN-MOBILE M-91, M-95, M-99): the header inbox badge (<c>Default</c>) and the rows of the
/// dashboard "Waiting for action" card (<c>Dashboard</c>). Hidden for users without the Approval Inbox menu.
/// </summary>
public sealed class ApprovalBadgeViewComponent(
    IMenuRights menuRights,
    IQueryHandler<GetApprovalInboxCountQuery, ApprovalInboxCountResponse> countQuery,
    IQueryHandler<GetApprovalRequestsQuery, PagedList<ApprovalRequestResponse>> requestsQuery) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string view = "Default")
    {
        if (!await menuRights.CanAsync(MenuCodes.ApprovalsInbox, Domain.Access.MenuRights.View))
        {
            return Content(string.Empty);
        }

        Result<ApprovalInboxCountResponse> count = await countQuery.Handle(new GetApprovalInboxCountQuery(), HttpContext.RequestAborted);

        int? stuck = null;
        if (view == "Dashboard" && await menuRights.CanAsync(MenuCodes.ApprovalsRequests, Domain.Access.MenuRights.View))
        {
            Result<PagedList<ApprovalRequestResponse>> stuckRequests = await requestsQuery.Handle(
                new GetApprovalRequestsQuery(new PageRequest(1, 1), StuckOnly: true), HttpContext.RequestAborted);
            stuck = stuckRequests.IsSuccess ? (int)stuckRequests.Value.TotalCount : null;
        }

        return View(view, new ApprovalBadgeModel(count.IsSuccess ? count.Value.Total : 0, stuck));
    }
}
