using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Approvals;
using Domain.Access;
using Domain.Approvals;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.Approvals.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Areas.Approvals.Controllers;

/// <summary>
/// Every approval request of the user's branches (M-91), with the "stuck" filter (M-99) and replacing an approver of
/// a level still to be decided on one request (M-32).
/// </summary>
[Area("Approvals")]
[MenuAccess(MenuCodes.ApprovalsRequests)]
public sealed class RequestsController(
    PageSupport support,
    IQueryHandler<GetApprovalRequestsQuery, PagedList<ApprovalRequestResponse>> requestsQuery,
    IQueryHandler<GetApprovalRequestQuery, ApprovalDetailResponse> requestQuery) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? branch,
        ApprovalStatus? status,
        ApprovalDocumentType? type,
        bool? stuck,
        int? page,
        CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<ApprovalRequestResponse>> rows = await requestsQuery.Handle(
            new GetApprovalRequestsQuery(
                new PageRequest(page, PageRequest.DefaultPageSize, search), status, type, branchFilter.BranchId, stuck == true),
            cancellationToken);

        return View(new ApprovalListViewModel
        {
            Rows = rows.Value,
            Search = search,
            DocumentType = type?.ToString(),
            Status = status?.ToString(),
            Branch = branch,
            StuckOnly = stuck == true,
            BranchOptions = branchFilter.Options
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        [FromServices] IQueryHandler<GetApproverCandidatesQuery, IReadOnlyList<ApproverCandidateResponse>> candidatesQuery,
        CancellationToken cancellationToken)
    {
        Result<ApprovalDetailResponse> result = await requestQuery.Handle(new GetApprovalRequestQuery(id, AnyInBranch: true), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        bool canReassign = result.Value.Request.Status == nameof(ApprovalStatus.Pending)
            && await support.CanAsync(MenuCodes.ApprovalsRequests, MenuRights.Edit);

        IReadOnlyList<SelectListItem> candidates = [];
        if (canReassign)
        {
            Result<IReadOnlyList<ApproverCandidateResponse>> users =
                await candidatesQuery.Handle(new GetApproverCandidatesQuery(result.Value.Request.BranchId), cancellationToken);
            candidates = [.. users.Value.Select(u => new SelectListItem(u.CoversBranch ? u.Name : $"{u.Name} (no branch access)", u.Id.ToString()))];
        }

        return View("~/Areas/Approvals/Views/Inbox/Details.cshtml", new ApprovalDetailsViewModel(
            result.Value, canReassign, candidates, Url.Action(nameof(Index))!));
    }

    [HttpPost]
    [MenuAccess(MenuCodes.ApprovalsRequests, MenuRights.Edit)]
    public async Task<IActionResult> Reassign(
        Guid id,
        int levelNo,
        Guid fromUserId,
        Guid toUserId,
        [FromServices] ICommandHandler<ReassignApproverCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new ReassignApproverCommand(id, levelNo, fromUserId, toUserId), cancellationToken);

        if (result.IsSuccess)
        {
            NotifySuccess($"The approver of level {levelNo} has been replaced.");
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
