using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Approvals;
using Application.Branches;
using Application.Branches.Create;
using Application.Branches.Get;
using Application.Branches.GetById;
using Application.Branches.Update;
using Domain.Access;
using Domain.Approvals;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Admin.Models;
using Web.App.Areas.Approvals.Models;
using Web.App.Controllers;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Areas.Admin.Controllers;

/// <summary>
/// Branch master data. Deactivating a branch removes it from every user's effective branches. A new branch can get its
/// own approval flows right away (PLAN-MOBILE M-97); branches without a flow of a document type use the default one.
/// </summary>
[Area("Admin")]
[MenuAccess(MenuCodes.AdminBranches)]
public sealed class BranchesController(
    IQueryHandler<GetApprovalFlowsQuery, IReadOnlyList<ApprovalFlowResponse>> flowsQuery,
    IQueryHandler<GetApproverCandidatesQuery, IReadOnlyList<ApproverCandidateResponse>> candidatesQuery,
    ICommandHandler<SetUpBranchApprovalFlowsCommand, int> setUpFlows) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? page,
        [FromServices] IQueryHandler<GetBranchesQuery, PagedList<BranchResponse>> query,
        CancellationToken cancellationToken)
    {
        Result<PagedList<BranchResponse>> result = await query.Handle(
            new GetBranchesQuery(new PageRequest(page, PageRequest.DefaultPageSize, search)), cancellationToken);
        Result<IReadOnlyList<ApprovalFlowResponse>> flows = await flowsQuery.Handle(new GetApprovalFlowsQuery(), cancellationToken);

        ViewData["Search"] = search;
        ViewData["ApprovalFlows"] = flows.IsSuccess ? flows.Value : [];
        return View(result.Value);
    }

    [HttpGet]
    [MenuAccess(MenuCodes.AdminBranches, MenuRights.Create)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        // The levels of the default flow are the starting point of the branch flow.
        Result<IReadOnlyList<ApprovalFlowResponse>> flows = await flowsQuery.Handle(new GetApprovalFlowsQuery(), cancellationToken);
        ApprovalFlowResponse? template = flows.IsSuccess
            ? flows.Value.FirstOrDefault(f => f.BranchId == null && f.IsActive && f.DocumentType == nameof(ApprovalDocumentType.Farmer))
              ?? flows.Value.FirstOrDefault(f => f.BranchId == null && f.IsActive)
            : null;

        var model = new BranchFormViewModel
        {
            IsActive = true,
            ApprovalSetup = "default",
            ApprovalLevels = template is null ? [new ApprovalLevelRow()] : ApprovalLevelRow.From(template.Levels),
            ApprovalDocumentTypes = [.. Enum.GetValues<ApprovalDocumentType>()],
            GrantBranchAccess = true
        };

        return View("Form", await WithApproverNamesAsync(model, cancellationToken));
    }

    [HttpPost]
    [MenuAccess(MenuCodes.AdminBranches, MenuRights.Create)]
    public async Task<IActionResult> Create(
        BranchFormViewModel model,
        [FromServices] ICommandHandler<CreateBranchCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        bool ownFlows = model.ApprovalSetup == "branch";
        if (ownFlows)
        {
            ValidateApprovalSetup(model);
        }
        else
        {
            // The level rows are only posted for the branch's own flows.
            foreach (string key in ModelState.Keys.Where(k => k.StartsWith(nameof(BranchFormViewModel.ApprovalLevels), StringComparison.Ordinal)).ToList())
            {
                ModelState.Remove(key);
            }
        }

        if (!ModelState.IsValid)
        {
            return View("Form", await WithApproverNamesAsync(model, cancellationToken));
        }

        Result<Guid> result = await handler.Handle(
            new CreateBranchCommand(model.Code, model.Name, model.Address, model.Phone), cancellationToken);

        if (result.IsFailure)
        {
            AddErrors(result.Error);
            return View("Form", await WithApproverNamesAsync(model, cancellationToken));
        }

        if (!ownFlows)
        {
            NotifySuccess($"Branch {model.Code} has been created; it uses the default approval flows.");
            return RedirectToAction(nameof(Index));
        }

        Result<int> flows = await setUpFlows.Handle(
            new SetUpBranchApprovalFlowsCommand(
                result.Value, model.ApprovalDocumentTypes, ApprovalLevelRow.ToDefinitions(model.ApprovalLevels), model.GrantBranchAccess == true),
            cancellationToken);

        if (flows.IsFailure)
        {
            NotifyError($"Branch {model.Code} has been created, but its approval flows were not: {flows.Error.Description}");
            return RedirectToAction(nameof(Edit), new { id = result.Value });
        }

        NotifySuccess($"Branch {model.Code} has been created with {flows.Value} approval flow(s).");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [MenuAccess(MenuCodes.AdminBranches, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        Guid id,
        [FromServices] IQueryHandler<GetBranchByIdQuery, BranchResponse> query,
        CancellationToken cancellationToken)
    {
        Result<BranchResponse> result = await query.Handle(new GetBranchByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        BranchResponse branch = result.Value;

        return View("Form", new BranchFormViewModel
        {
            Id = branch.Id,
            Code = branch.Code,
            Name = branch.Name,
            Address = branch.Address,
            Phone = branch.Phone,
            IsActive = branch.IsActive,
            ApprovalFlows = await FlowsOfBranchAsync(branch.Id, cancellationToken)
        });
    }

    [HttpPost]
    [MenuAccess(MenuCodes.AdminBranches, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        BranchFormViewModel model,
        [FromServices] ICommandHandler<UpdateBranchCommand> handler,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.Id is not Guid id)
        {
            model.ApprovalFlows = model.Id is Guid branchId ? await FlowsOfBranchAsync(branchId, cancellationToken) : [];
            return View("Form", model);
        }

        Result result = await handler.Handle(
            new UpdateBranchCommand(id, model.Name, model.Address, model.Phone, model.IsActive == true), cancellationToken);

        if (result.IsFailure)
        {
            AddErrors(result.Error);
            model.ApprovalFlows = await FlowsOfBranchAsync(id, cancellationToken);
            return View("Form", model);
        }

        NotifySuccess($"Branch {model.Code} has been saved.");
        return RedirectToAction(nameof(Index));
    }

    private void ValidateApprovalSetup(BranchFormViewModel model)
    {
        if (model.ApprovalDocumentTypes.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Choose at least one document type for the branch's approval flows.");
        }

        if (model.ApprovalLevels.Count is 0 or > ApprovalFlow.MaxLevels)
        {
            ModelState.AddModelError(string.Empty, $"An approval flow needs 1 to {ApprovalFlow.MaxLevels} levels.");
        }

        for (int i = 0; i < model.ApprovalLevels.Count; i++)
        {
            if (model.ApprovalLevels[i].ApproverUserIds.Count == 0)
            {
                ModelState.AddModelError(string.Empty, $"Level {i + 1} needs at least one approver.");
            }
        }
    }

    private async Task<BranchFormViewModel> WithApproverNamesAsync(BranchFormViewModel model, CancellationToken cancellationToken)
    {
        HashSet<Guid> ids = [.. model.ApprovalLevels.SelectMany(l => l.ApproverUserIds)];
        if (ids.Count > 0)
        {
            Result<IReadOnlyList<ApproverCandidateResponse>> users =
                await candidatesQuery.Handle(new GetApproverCandidatesQuery(), cancellationToken);
            model.ApproverNames = users.Value.Where(u => ids.Contains(u.Id)).ToDictionary(u => u.Id, u => u.Name);
        }

        return model;
    }

    /// <summary>
    /// The flow used by the branch per document type: its own active flow, else the active default flow.
    /// </summary>
    private async Task<IReadOnlyList<ApprovalFlowResponse>> FlowsOfBranchAsync(Guid branchId, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ApprovalFlowResponse>> flows = await flowsQuery.Handle(new GetApprovalFlowsQuery(), cancellationToken);
        if (flows.IsFailure)
        {
            return [];
        }

        return
        [
            .. Enum.GetValues<ApprovalDocumentType>()
                .Select(type => flows.Value.FirstOrDefault(f => f.IsActive && f.BranchId == branchId && f.DocumentType == type.ToString())
                                ?? flows.Value.FirstOrDefault(f => f.IsActive && f.BranchId == null && f.DocumentType == type.ToString()))
                .OfType<ApprovalFlowResponse>()
        ];
    }
}
