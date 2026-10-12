using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Approvals;
using Domain.Approvals;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Approvals.Models;
using Web.App.Controllers;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Workflow;

namespace Web.App.Areas.Approvals.Controllers;

/// <summary>
/// Approval inbox (PLAN-MOBILE §5.4, M-91): pending requests whose current level the user may decide, as a listed
/// approver or a delegate. Who decides is set by the flow, not by menu rights: the menu only opens the page.
/// </summary>
[Area("Approvals")]
[MenuAccess(MenuCodes.ApprovalsInbox)]
public sealed class InboxController(
    IQueryHandler<GetApprovalInboxQuery, PagedList<ApprovalRequestResponse>> inboxQuery,
    IQueryHandler<GetApprovalInboxCountQuery, ApprovalInboxCountResponse> countQuery,
    IQueryHandler<GetApprovalRequestQuery, ApprovalDetailResponse> requestQuery) : AppController
{
    private const string DocumentType = "ApprovalRequest";

    [HttpGet]
    public async Task<IActionResult> Index(string? search, ApprovalDocumentType? type, int? page, CancellationToken cancellationToken)
    {
        Result<PagedList<ApprovalRequestResponse>> rows = await inboxQuery.Handle(
            new GetApprovalInboxQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), type), cancellationToken);
        Result<ApprovalInboxCountResponse> counts = await countQuery.Handle(new GetApprovalInboxCountQuery(), cancellationToken);

        return View(new ApprovalListViewModel
        {
            Rows = rows.Value,
            Search = search,
            DocumentType = type?.ToString(),
            Counts = counts.Value.ByDocumentType
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<ApprovalDetailResponse> result = await requestQuery.Handle(new GetApprovalRequestQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        return View(new ApprovalDetailsViewModel(result.Value, CanReassign: false, [], Url.Action(nameof(Index))!));
    }

    [HttpPost]
    [WorkflowAction("approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        string? note,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<ApproveRequestCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "approve"),
            ct => handler.Handle(new ApproveRequestCommand(id, note), ct),
            cancellationToken);

        return AfterDecision(id, result, "The request has been approved.");
    }

    [HttpPost]
    [WorkflowAction("reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        string? reason,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<RejectRequestCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "reject"),
            ct => handler.Handle(new RejectRequestCommand(id, reason ?? string.Empty), ct),
            cancellationToken);

        return AfterDecision(id, result, "The request has been rejected.");
    }

    private RedirectToActionResult AfterDecision(Guid id, Result result, string successMessage)
    {
        if (result.IsSuccess)
        {
            NotifySuccess(successMessage);
            return RedirectToAction(nameof(Index));
        }

        NotifyError(result.Error.Description);
        return RedirectToAction(nameof(Details), new { id });
    }
}
