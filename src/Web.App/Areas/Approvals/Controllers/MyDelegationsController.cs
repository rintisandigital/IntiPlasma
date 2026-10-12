using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Approvals;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.Approvals.Models;
using Web.App.Controllers;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Areas.Approvals.Controllers;

/// <summary>
/// The signed-in approver's own delegations while on leave (M-98). Open to every signed-in user; only approvers can
/// delegate.
/// </summary>
[Area("Approvals")]
[AuthenticatedOnly]
public sealed class MyDelegationsController : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        bool? ended,
        [FromServices] IQueryHandler<GetApprovalDelegationsQuery, IReadOnlyList<ApprovalDelegationResponse>> query,
        [FromServices] IQueryHandler<GetApproverCandidatesQuery, IReadOnlyList<ApproverCandidateResponse>> candidates,
        [FromServices] IUserContext userContext,
        CancellationToken cancellationToken) =>
        View(await DelegationPage.BuildAsync(query, candidates, userContext.UserId, allUsers: false, ended == true, new DelegationFormViewModel(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Form")] DelegationFormViewModel model,
        [FromServices] ICommandHandler<CreateApprovalDelegationCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        Result result = await DelegationPage.CreateAsync(handler, model, null, cancellationToken);

        if (result.IsSuccess)
        {
            NotifySuccess("Your approvals are delegated for the chosen period.");
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Revoke(
        Guid id,
        [FromServices] ICommandHandler<RevokeApprovalDelegationCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new RevokeApprovalDelegationCommand(id), cancellationToken);

        if (result.IsSuccess)
        {
            NotifySuccess("The delegation has been revoked.");
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Index));
    }
}
