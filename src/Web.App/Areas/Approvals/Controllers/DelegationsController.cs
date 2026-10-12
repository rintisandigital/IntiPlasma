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
/// Delegations of every approver (M-98, administration): delegate on behalf of an approver who is away, revoke.
/// </summary>
[Area("Approvals")]
[MenuAccess(MenuCodes.ApprovalsDelegations)]
public sealed class DelegationsController : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        bool? ended,
        [FromServices] IQueryHandler<GetApprovalDelegationsQuery, IReadOnlyList<ApprovalDelegationResponse>> query,
        [FromServices] IQueryHandler<GetApproverCandidatesQuery, IReadOnlyList<ApproverCandidateResponse>> candidates,
        [FromServices] IUserContext userContext,
        CancellationToken cancellationToken) =>
        View(await DelegationPage.BuildAsync(query, candidates, userContext.UserId, allUsers: true, ended == true, new DelegationFormViewModel(), cancellationToken));

    [HttpPost]
    [MenuAccess(MenuCodes.ApprovalsDelegations, MenuRights.Create)]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Form")] DelegationFormViewModel model,
        [FromServices] ICommandHandler<CreateApprovalDelegationCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        Result result = model.DelegatorUserId is null
            ? Result.Failure(Error.Problem("ApprovalDelegations.DelegatorRequired", "Choose the approver who delegates."))
            : await DelegationPage.CreateAsync(handler, model, model.DelegatorUserId, cancellationToken);

        Notify(result, "The delegation has been saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [MenuAccess(MenuCodes.ApprovalsDelegations, MenuRights.Edit)]
    public async Task<IActionResult> Revoke(
        Guid id,
        [FromServices] ICommandHandler<RevokeApprovalDelegationCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new RevokeApprovalDelegationCommand(id, AnyUser: true), cancellationToken);

        Notify(result, "The delegation has been revoked.");
        return RedirectToAction(nameof(Index));
    }

    private void Notify(Result result, string success)
    {
        if (result.IsSuccess)
        {
            NotifySuccess(success);
        }
        else
        {
            NotifyError(result.Error.Description);
        }
    }
}

internal static class DelegationPage
{
    public static async Task<DelegationsViewModel> BuildAsync(
        IQueryHandler<GetApprovalDelegationsQuery, IReadOnlyList<ApprovalDelegationResponse>> query,
        IQueryHandler<GetApproverCandidatesQuery, IReadOnlyList<ApproverCandidateResponse>> candidatesQuery,
        Guid currentUserId,
        bool allUsers,
        bool includeEnded,
        DelegationFormViewModel form,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ApprovalDelegationResponse>> delegations =
            await query.Handle(new GetApprovalDelegationsQuery(allUsers, includeEnded), cancellationToken);
        Result<IReadOnlyList<ApproverCandidateResponse>> candidates =
            await candidatesQuery.Handle(new GetApproverCandidatesQuery(), cancellationToken);

        return new DelegationsViewModel(
            delegations.Value,
            form,
            [.. candidates.Value.Select(c => new SelectListItem($"{c.Name} ({c.Email})", c.Id.ToString()))],
            allUsers,
            includeEnded,
            CanDelegate: candidates.Value.Count > 1 && (allUsers || candidates.Value.Any(c => c.Id == currentUserId)),
            currentUserId);
    }

    public static async Task<Result> CreateAsync(
        ICommandHandler<CreateApprovalDelegationCommand, Guid> handler,
        DelegationFormViewModel model,
        Guid? delegatorUserId,
        CancellationToken cancellationToken)
    {
        if (model.DelegateUserId is null || model.FromDate is null || model.ToDate is null)
        {
            return Result.Failure(Error.Problem("ApprovalDelegations.Incomplete", "Choose the delegate and the period."));
        }

        return await handler.Handle(
            new CreateApprovalDelegationCommand(model.DelegateUserId.Value, model.FromDate.Value, model.ToDate.Value, model.Reason, delegatorUserId),
            cancellationToken);
    }
}
