using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Approvals;
using Domain.Approvals;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Approvals.Models;
using Web.App.Controllers;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Areas.Approvals.Controllers;

/// <summary>
/// Requests submitted by the signed-in user ("Pengajuan saya"); any signed-in user may open it and cancel their
/// own pending requests (M-83).
/// </summary>
[Area("Approvals")]
[AuthenticatedOnly]
public sealed class MySubmissionsController : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        ApprovalStatus? status,
        int? page,
        [FromServices] IQueryHandler<GetMySubmissionsQuery, PagedList<ApprovalRequestResponse>> query,
        CancellationToken cancellationToken)
    {
        Result<PagedList<ApprovalRequestResponse>> rows = await query.Handle(
            new GetMySubmissionsQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), status), cancellationToken);

        return View(new ApprovalListViewModel { Rows = rows.Value, Search = search, Status = status?.ToString() });
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        [FromServices] IQueryHandler<GetApprovalRequestQuery, ApprovalDetailResponse> query,
        CancellationToken cancellationToken)
    {
        Result<ApprovalDetailResponse> result = await query.Handle(new GetApprovalRequestQuery(id), cancellationToken);

        return result.IsFailure
            ? NotFound()
            : View("~/Areas/Approvals/Views/Inbox/Details.cshtml", new ApprovalDetailsViewModel(result.Value, false, [], Url.Action(nameof(Index))!));
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(
        Guid id,
        string? returnUrl,
        [FromServices] ICommandHandler<CancelRequestCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new CancelRequestCommand(id), cancellationToken);

        if (result.IsSuccess)
        {
            NotifySuccess("The request has been cancelled; the document is a draft again.");
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }
}
