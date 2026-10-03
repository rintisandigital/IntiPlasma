using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Contracts;
using Application.Contracts.ChangeStatus;
using Application.Contracts.Create;
using Application.Contracts.Get;
using Application.Contracts.GetById;
using Application.Contracts.Update;
using Application.Documents;
using Application.TaxCodes.Get;
using Domain.Access;
using Domain.Documents.Attachments;
using Domain.Partnership.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.MasterData.Models;
using Web.App.Areas.Partnership.Documents;
using Web.App.Areas.Partnership.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;
using Web.App.Infrastructure.Workflow;

namespace Web.App.Areas.Partnership.Controllers;

/// <summary>
/// Partnership contracts (keputusan #1): price contract (sapronak contract prices + guaranteed live bird prices
/// per weight range) or profit sharing (plasma share %), with bonuses/deductions. Draft → Active → Inactive;
/// only drafts can be edited.
/// </summary>
[Area("Partnership")]
[MenuAccess(MenuCodes.PartnershipContracts)]
public sealed class ContractsController(
    PageSupport support,
    IQueryHandler<GetContractsQuery, PagedList<ContractResponse>> contractsQuery,
    IQueryHandler<GetContractByIdQuery, ContractResponse> contractQuery,
    IQueryHandler<GetTaxCodesQuery, IReadOnlyList<TaxCodeResponse>> taxCodesQuery) : AppController
{
    private const string MenuCode = MenuCodes.PartnershipContracts;
    private const string DocumentType = "Contract";

    private static readonly ExportColumn<ContractResponse>[] Columns =
    [
        new("Code", c => c.Code, Width: 1.2f),
        new("Name", c => c.Name, Width: 3),
        new("Branch", c => c.BranchCode, Width: 0.8f),
        new("Scheme", c => EnumOptions.Label(c.Scheme), Width: 1.3f),
        new("Status", c => c.Status, Width: 0.9f),
        new("Valid from", c => c.ValidFrom, ExportFormat.Date, 1),
        new("Valid to", c => c.ValidTo, ExportFormat.Date, 1),
        new("Plasma share %", c => c.PlasmaProfitSharePercent, ExportFormat.Percent, 1),
        new("PPh", c => c.IncomeTaxCode, Width: 0.9f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? branch, ContractStatus? status, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<ContractResponse>> result = await contractsQuery.Handle(
            new GetContractsQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, status),
            cancellationToken);

        return View(new ListViewModel<ContractResponse>
        {
            Rows = result.Value,
            Search = search,
            BranchOptions = branchFilter.Options,
            FilterOptions = new Dictionary<string, IReadOnlyList<SelectListItem>> { ["status"] = EnumOptions.For(status) }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, string? search, string? branch, ContractStatus? status, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        List<string> filters = [branchFilter.Description];
        if (status is not null)
        {
            filters.Add($"Status: {status}");
        }

        return await support.ExportAsync(format, "Partnership Contracts", "contracts", filters, Columns,
            (paging, ct) => contractsQuery.Handle(new GetContractsQuery(paging, branchFilter.BranchId, status), ct), search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<ContractResponse> result = await contractQuery.Handle(new GetContractByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        ContractResponse contract = result.Value;
        bool canEdit = await support.CanAsync(MenuCode, MenuRights.Edit);

        return View(new ContractDetailsViewModel(
            contract,
            await support.AttachmentsAsync(contract.Documents, cancellationToken),
            CanEditDraft: canEdit && contract.Status == nameof(ContractStatus.Draft),
            CanChangeStatus: canEdit,
            CanManageAttachments: canEdit));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("Form", await WithOptionsAsync(
            new ContractFormViewModel { Scheme = ContractScheme.PriceContract, ValidFrom = DateOnly.FromDateTime(DateTime.Today) },
            cancellationToken));

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        ContractFormViewModel model,
        [FromServices] ICommandHandler<CreateContractCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateContractCommand(model.Code, model.BranchId!.Value, model.Scheme!.Value, model.ToTerms(), model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Contract {model.Code} has been created as a draft.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        Result<ContractResponse> result = await contractQuery.Handle(new GetContractByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        if (result.Value.Status != nameof(ContractStatus.Draft))
        {
            NotifyError("Only draft contracts can be changed.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View("Form", await WithOptionsAsync(ContractFormViewModel.From(result.Value), cancellationToken));
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        ContractFormViewModel model,
        [FromServices] ICommandHandler<UpdateContractCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(new UpdateContractCommand(id, model.ToTerms(), model.Documents), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Contract {model.Code} has been saved.");
                return RedirectToAction(nameof(Details), new { id });
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("activate")]
    public async Task<IActionResult> Activate(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<ActivateContractCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "activate"),
            ct => handler.Handle(new ActivateContractCommand(id), ct),
            cancellationToken);

        return AfterAction(id, result, "The contract is now active and can be used by new cycles.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<DeactivateContractCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "deactivate"),
            ct => handler.Handle(new DeactivateContractCommand(id), ct),
            cancellationToken);

        return AfterAction(id, result, "The contract has been deactivated; running cycles keep their snapshot.");
    }

    /// <summary>
    /// Replaces the attachments of a contract in any status (only the attachments change).
    /// </summary>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Documents(
        Guid id,
        List<Guid> documents,
        [FromServices] ICommandHandler<SetDocumentsCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(
            new SetDocumentsCommand(AttachmentOwnerTypes.Contract, id, documents), cancellationToken);

        return AfterAction(id, result, "The attachments have been saved.");
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Print(Guid id, [FromServices] DisplayFormatter formatter, CancellationToken cancellationToken)
    {
        Result<ContractResponse> result = await contractQuery.Handle(new GetContractByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        ContractResponse contract = result.Value;
        ExportHeader header = await support.Exports.HeaderAsync(
            "Partnership Contract", $"contract-{contract.Code}", [$"No. {contract.Code}", $"Status: {contract.Status}"]);

        return support.Exports.Document(header, container => ContractPdf.Compose(container, contract, formatter));
    }

    private RedirectToActionResult AfterAction(Guid id, Result result, string successMessage)
    {
        if (result.IsSuccess)
        {
            NotifySuccess(successMessage);
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<ContractFormViewModel> WithOptionsAsync(ContractFormViewModel model, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<TaxCodeResponse>> taxCodes = await taxCodesQuery.Handle(new GetTaxCodesQuery(), cancellationToken);

        model.IncomeTaxCodes = taxCodes.IsSuccess
            ? [.. taxCodes.Value
                .Where(t => t.Type == "IncomeTax" && (t.IsActive || t.Id == model.IncomeTaxCodeId))
                .Select(t => new SelectListItem($"{t.Code} — {t.Name}", t.Id.ToString()))]
            : [];
        model.Branches = await support.BranchOptionsAsync(model.BranchId);
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);

        return model;
    }
}
