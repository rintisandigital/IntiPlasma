using Application.Abstractions.Messaging;
using Application.Approvals;
using Domain.Access;
using Domain.Approvals;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.Admin.Models;
using Web.App.Areas.Approvals.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Areas.Admin.Controllers;

/// <summary>
/// Approval flows (PLAN-MOBILE §5, M-29 s.d. M-32, M-96): per document type a default flow for every branch and
/// optional branch flows, 1-5 levels each with one or more approvers. Changes only affect new requests. Also
/// replaces a departing approver everywhere at once (M-99).
/// </summary>
[Area("Admin")]
[MenuAccess(MenuCodes.AdminApprovalFlows)]
public sealed class ApprovalFlowsController(
    PageSupport support,
    IQueryHandler<GetApprovalFlowsQuery, IReadOnlyList<ApprovalFlowResponse>> flowsQuery,
    IQueryHandler<GetApprovalFlowByIdQuery, ApprovalFlowResponse> flowQuery,
    IQueryHandler<GetApproverCandidatesQuery, IReadOnlyList<ApproverCandidateResponse>> candidatesQuery) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(ApprovalDocumentType? type, string? branch, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<IReadOnlyList<ApprovalFlowResponse>> flows = await flowsQuery.Handle(new GetApprovalFlowsQuery(type), cancellationToken);

        IReadOnlyList<ApprovalFlowResponse> rows = branchFilter.BranchId is { } branchId
            ? [.. flows.Value.Where(f => f.BranchId == branchId || f.BranchId == null)]
            : flows.Value;

        return View(new ApprovalFlowListViewModel(rows, type, branch, branchFilter.Options));
    }

    [HttpGet]
    [MenuAccess(MenuCodes.AdminApprovalFlows, MenuRights.Create)]
    public async Task<IActionResult> Create(ApprovalDocumentType? type, Guid? branchId, Guid? copyFrom, CancellationToken cancellationToken)
    {
        var model = new ApprovalFlowFormViewModel { DocumentType = type, BranchId = branchId, IsActive = true };

        if (copyFrom is { } sourceId
            && await flowQuery.Handle(new GetApprovalFlowByIdQuery(sourceId), cancellationToken) is { IsSuccess: true } source)
        {
            model.DocumentType = Enum.Parse<ApprovalDocumentType>(source.Value.DocumentType);
            model.Levels = ApprovalLevelRow.From(source.Value.Levels);
        }

        if (model.Levels.Count == 0)
        {
            model.Levels = [new ApprovalLevelRow()];
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpPost]
    [MenuAccess(MenuCodes.AdminApprovalFlows, MenuRights.Create)]
    public async Task<IActionResult> Create(
        ApprovalFlowFormViewModel model,
        [FromServices] ICommandHandler<CreateApprovalFlowCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateApprovalFlowCommand(
                    model.DocumentType!.Value, model.BranchId, model.Name ?? string.Empty, ApprovalLevelRow.ToDefinitions(model.Levels)),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess("The approval flow has been created; it applies to new requests.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpGet]
    [MenuAccess(MenuCodes.AdminApprovalFlows, MenuRights.Edit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        Result<ApprovalFlowResponse> result = await flowQuery.Handle(new GetApprovalFlowByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        ApprovalFlowResponse flow = result.Value;

        return View("Form", await WithOptionsAsync(
            new ApprovalFlowFormViewModel
            {
                Id = flow.Id,
                DocumentType = Enum.Parse<ApprovalDocumentType>(flow.DocumentType),
                BranchId = flow.BranchId,
                Name = flow.Name,
                IsActive = flow.IsActive,
                Levels = ApprovalLevelRow.From(flow.Levels)
            },
            cancellationToken));
    }

    [HttpPost]
    [MenuAccess(MenuCodes.AdminApprovalFlows, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        ApprovalFlowFormViewModel model,
        [FromServices] ICommandHandler<UpdateApprovalFlowCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateApprovalFlowCommand(id, model.Name ?? string.Empty, model.IsActive == true, ApprovalLevelRow.ToDefinitions(model.Levels)),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess("The approval flow has been saved; running requests keep their levels.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    /// <summary>
    /// Preview of where a user is an approver, then the replacement (M-99).
    /// </summary>
    [HttpGet]
    [MenuAccess(MenuCodes.AdminApprovalFlows, MenuRights.Edit)]
    public async Task<IActionResult> ReplaceApprover(
        Guid? fromUserId,
        Guid? toUserId,
        Guid? branchId,
        [FromServices] IQueryHandler<GetApproverUsageQuery, ApproverUsageResponse> usageQuery,
        CancellationToken cancellationToken)
    {
        ApproverUsageResponse? usage = null;
        if (fromUserId is { } userId)
        {
            usage = (await usageQuery.Handle(new GetApproverUsageQuery(userId, branchId), cancellationToken)).Value;
        }

        IReadOnlyList<SelectListItem> approvers = await UserOptionsAsync(cancellationToken);

        return View(new ReplaceApproverViewModel(
            fromUserId,
            toUserId,
            branchId,
            usage,
            await FlowApproverOptionsAsync(approvers, cancellationToken),
            approvers,
            await support.BranchOptionsAsync(branchId ?? Guid.Empty)));
    }

    [HttpPost]
    [MenuAccess(MenuCodes.AdminApprovalFlows, MenuRights.Edit)]
    [ActionName("ReplaceApprover")]
    public async Task<IActionResult> ReplaceApproverPost(
        Guid fromUserId,
        Guid toUserId,
        Guid? branchId,
        [FromServices] ICommandHandler<ReplaceApproverCommand, ReplaceApproverResult> handler,
        CancellationToken cancellationToken)
    {
        Result<ReplaceApproverResult> result = await handler.Handle(
            new ReplaceApproverCommand(fromUserId, toUserId, branchId), cancellationToken);

        if (result.IsFailure)
        {
            NotifyError(result.Error.Description);
            return RedirectToAction(nameof(ReplaceApprover), new { fromUserId, toUserId, branchId });
        }

        NotifySuccess($"Approver replaced in {result.Value.Flows} flow(s) and {result.Value.Requests} pending request(s).");
        return RedirectToAction(nameof(ReplaceApprover), new { fromUserId = toUserId, branchId });
    }

    private async Task<ApprovalFlowFormViewModel> WithOptionsAsync(ApprovalFlowFormViewModel model, CancellationToken cancellationToken)
    {
        model.BranchOptions = await support.BranchOptionsAsync(model.BranchId ?? Guid.Empty);
        model.ApproverNames = await ApproverNamesAsync(model.Levels.SelectMany(l => l.ApproverUserIds), cancellationToken);

        return model;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ApproverNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        HashSet<Guid> wanted = [.. ids];
        if (wanted.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        Result<IReadOnlyList<ApproverCandidateResponse>> users =
            await candidatesQuery.Handle(new GetApproverCandidatesQuery(), cancellationToken);

        return users.Value.Where(u => wanted.Contains(u.Id)).ToDictionary(u => u.Id, u => u.Name);
    }

    /// <summary>
    /// Users who can be chosen as the replacement: active approvers.
    /// </summary>
    private async Task<IReadOnlyList<SelectListItem>> UserOptionsAsync(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ApproverCandidateResponse>> users =
            await candidatesQuery.Handle(new GetApproverCandidatesQuery(), cancellationToken);

        return [.. users.Value.Select(u => new SelectListItem($"{u.Name} ({u.Email})", u.Id.ToString()))];
    }

    /// <summary>
    /// Users who can be replaced: every approver listed in a flow, also inactive ones (resigned), plus the active
    /// approvers.
    /// </summary>
    private async Task<IReadOnlyList<SelectListItem>> FlowApproverOptionsAsync(
        IReadOnlyList<SelectListItem> approvers,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ApprovalFlowResponse>> flows = await flowsQuery.Handle(new GetApprovalFlowsQuery(), cancellationToken);

        IEnumerable<SelectListItem> listed = flows.Value
            .SelectMany(f => f.Levels)
            .SelectMany(l => l.Approvers)
            .Where(a => !a.CanDecide)
            .DistinctBy(a => a.UserId)
            .Select(a => new SelectListItem($"{a.Name} (cannot decide)", a.UserId.ToString()));

        return [.. listed.Concat(approvers).DistinctBy(o => o.Value).OrderBy(o => o.Text, StringComparer.OrdinalIgnoreCase)];
    }
}
