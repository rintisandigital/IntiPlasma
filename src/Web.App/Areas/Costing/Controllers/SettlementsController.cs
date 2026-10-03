using Application.Abstractions.Messaging;
using Application.Costing;
using Application.Cycles;
using Application.Cycles.GetById;
using Application.Documents;
using Domain.Access;
using Domain.Costing.PlasmaSettlements;
using Domain.Documents.Attachments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.Costing.Documents;
using Web.App.Areas.Costing.Models;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;
using Web.App.Infrastructure.Workflow;

namespace Web.App.Areas.Costing.Controllers;

/// <summary>
/// Plasma settlements (perhitungan hasil) of closed plasma cycles: Draft (maker; recalculable) → Approved by someone
/// else (checker; journaled, cycle Settled) → paid through plasma payment vouchers. A loss becomes the plasma's debt.
/// </summary>
[Area("Costing")]
[MenuAccess(MenuCodes.CostingSettlements)]
public sealed class SettlementsController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetPlasmaSettlementsQuery, IReadOnlyList<PlasmaSettlementResponse>> settlementsQuery,
    IQueryHandler<GetPlasmaSettlementByIdQuery, PlasmaSettlementResponse> settlementQuery,
    IQueryHandler<GetCycleByIdQuery, CycleResponse> cycleQuery,
    IQueryHandler<GetFarmerPlasmaDebtQuery, FarmerPlasmaDebtResponse> debtQuery) : AppController
{
    private const string MenuCode = MenuCodes.CostingSettlements;
    private const string DocumentType = "PlasmaSettlement";

    private static readonly ExportColumn<PlasmaSettlementResponse>[] Columns =
    [
        new("Number", s => s.Number, Width: 1.5f),
        new("Date", s => s.SettlementDate, ExportFormat.Date, 0.9f),
        new("Branch", s => s.BranchCode, Width: 0.7f),
        new("Cycle", s => s.CycleNumber, Width: 1.5f),
        new("Farmer", s => $"{s.FarmerCode} — {s.FarmerName}", Width: 1.8f),
        new("Scheme", s => SettlementLines.Scheme(s.Scheme), Width: 1),
        new("Gross income", s => s.GrossIncome, ExportFormat.Money, 1.3f),
        new("Income tax", s => s.IncomeTaxAmount, ExportFormat.Money, 1.1f),
        new("Debt deduction", s => s.DebtDeduction, ExportFormat.Money, 1.1f),
        new("Net payable", s => s.NetPayable, ExportFormat.Money, 1.3f),
        new("Loss (plasma debt)", s => s.Deficit, ExportFormat.Money, 1.2f),
        new("Paid", s => s.PaidAmount, ExportFormat.Money, 1.2f),
        new("Outstanding", s => s.Outstanding, ExportFormat.Money, 1.2f),
        new("Status", s => s.Status, Width: 0.9f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? branch, PlasmaSettlementStatus? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<IReadOnlyList<PlasmaSettlementResponse>> result = await settlementsQuery.Handle(
            new GetPlasmaSettlementsQuery(branchFilter.BranchId, null, null, status, search, from, to), cancellationToken);

        IReadOnlyList<PlasmaSettlementResponse> rows = result.IsSuccess ? result.Value : [];

        return View(new ListViewModel<PlasmaSettlementResponse>
        {
            Rows = new PagedList<PlasmaSettlementResponse>(rows, 1, Math.Max(rows.Count, 1), rows.Count),
            Search = search,
            BranchOptions = branchFilter.Options,
            Filters = DocumentLists.DateFilters(from, to),
            FilterOptions = new Dictionary<string, IReadOnlyList<SelectListItem>> { ["status"] = EnumOptions.For(status) }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(
        string? format, string? search, string? branch, PlasmaSettlementStatus? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<IReadOnlyList<PlasmaSettlementResponse>> result = await settlementsQuery.Handle(
            new GetPlasmaSettlementsQuery(branchFilter.BranchId, null, null, status, search, from, to), cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error.Description);
        }

        List<string> filters = DocumentLists.Filters(branchFilter, status?.ToString(), from, to, formatter);
        if (!string.IsNullOrWhiteSpace(search))
        {
            filters.Add($"Search: {search}");
        }

        return await support.ExportAsync(format, "Plasma Settlements", "plasma-settlements", filters, Columns, result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<PlasmaSettlementResponse> result = await settlementQuery.Handle(new GetPlasmaSettlementByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        PlasmaSettlementResponse settlement = result.Value;
        FarmerPlasmaDebtResponse? debt = settlement.Status == nameof(PlasmaSettlementStatus.Draft)
            ? await FarmerDebtAsync(settlement.FarmerId, cancellationToken)
            : null;

        return View(new SettlementDetailsViewModel(
            settlement,
            await support.AttachmentsAsync(settlement.Documents, cancellationToken),
            debt,
            CanEdit: await support.CanAsync(MenuCode, MenuRights.Edit),
            CanPrint: await support.CanAsync(MenuCode, MenuRights.Export),
            CanPay: await support.CanAsync(MenuCodes.FinancePaymentVouchers, MenuRights.Create)));
    }

    /// <param name="cycleId">The closed plasma cycle to settle (chosen with the lookup or from the cycle's page).</param>
    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(Guid? cycleId, CancellationToken cancellationToken)
    {
        var model = new SettlementFormViewModel
        {
            CycleId = cycleId,
            SettlementDate = formatter.Today(),
            DebtDeduction = 0
        };
        await PrepareAsync(model, cancellationToken);

        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        SettlementFormViewModel model,
        [FromServices] ICommandHandler<CreatePlasmaSettlementCommand, CreatePlasmaSettlementResponse> handler,
        CancellationToken cancellationToken)
    {
        model.Id = null;
        if (ModelState.IsValid)
        {
            Result<CreatePlasmaSettlementResponse> result = await handler.Handle(
                new CreatePlasmaSettlementCommand(
                    model.CycleId!.Value, model.SettlementDate!.Value, model.DebtDeduction!.Value, model.Notes, model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Settlement {result.Value.Number} has been calculated as a draft; another user must approve it.");
                return RedirectToAction(nameof(Details), new { id = result.Value.Id });
            }

            AddErrors(result.Error);
        }

        await PrepareAsync(model, cancellationToken);
        return View("Form", model);
    }

    /// <summary>
    /// Recalculates a draft from the cycle's current figures, with a new date, debt deduction or notes.
    /// </summary>
    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Recalculate(Guid id, CancellationToken cancellationToken)
    {
        Result<PlasmaSettlementResponse> result = await settlementQuery.Handle(new GetPlasmaSettlementByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        PlasmaSettlementResponse settlement = result.Value;
        if (settlement.Status != nameof(PlasmaSettlementStatus.Draft))
        {
            NotifyError("Only a draft settlement can be recalculated.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new SettlementFormViewModel
        {
            Id = id,
            CycleId = settlement.CycleId,
            SettlementDate = settlement.SettlementDate,
            DebtDeduction = settlement.DebtDeduction,
            Notes = settlement.Notes,
            Documents = [.. settlement.Documents]
        };
        await PrepareAsync(model, cancellationToken);

        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Recalculate(
        Guid id,
        SettlementFormViewModel model,
        [FromServices] ICommandHandler<RecalculatePlasmaSettlementCommand> handler,
        CancellationToken cancellationToken)
    {
        model.Id = id;
        if (ModelState.IsValid)
        {
            Result result = await handler.Handle(
                new RecalculatePlasmaSettlementCommand(id, model.SettlementDate!.Value, model.DebtDeduction!.Value, model.Notes, model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess("The settlement has been recalculated.");
                return RedirectToAction(nameof(Details), new { id });
            }

            AddErrors(result.Error);
        }

        await PrepareAsync(model, cancellationToken);
        return View("Form", model);
    }

    /// <summary>
    /// Checker approval: the domain refuses the maker approving their own settlement.
    /// </summary>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<ApprovePlasmaSettlementCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "approve"),
            ct => handler.Handle(new ApprovePlasmaSettlementCommand(id), ct),
            cancellationToken);

        return AfterAction(id, result, "The settlement has been approved and journaled; the cycle is settled.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        string? reason,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<CancelPlasmaSettlementCommand> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            NotifyError("Enter the reason for cancelling.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "cancel"),
            ct => handler.Handle(new CancelPlasmaSettlementCommand(id, reason), ct),
            cancellationToken);

        return AfterAction(id, result, "The settlement has been cancelled; the cycle can be settled again.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Documents(
        Guid id,
        List<Guid> documents,
        [FromServices] ICommandHandler<SetDocumentsCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new SetDocumentsCommand(AttachmentOwnerTypes.PlasmaSettlement, id, documents), cancellationToken);
        return AfterAction(id, result, "The attachments have been saved.");
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Print(Guid id, CancellationToken cancellationToken)
    {
        Result<PlasmaSettlementResponse> result = await settlementQuery.Handle(new GetPlasmaSettlementByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        PlasmaSettlementResponse settlement = result.Value;
        string title = settlement.Status is nameof(PlasmaSettlementStatus.Draft) ? "Plasma Settlement (DRAFT)" : "Plasma Settlement";
        ExportHeader header = await support.Exports.HeaderAsync(title, $"settlement-{settlement.Number}", [$"No. {settlement.Number}"]);

        return support.Exports.Document(header, container => CostingDocumentPdf.Settlement(container, settlement, formatter));
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

    private async Task PrepareAsync(SettlementFormViewModel model, CancellationToken cancellationToken)
    {
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);

        if (model.Id is Guid id)
        {
            Result<PlasmaSettlementResponse> settlement = await settlementQuery.Handle(new GetPlasmaSettlementByIdQuery(id), cancellationToken);
            model.Number = settlement.IsSuccess ? settlement.Value.Number : null;
        }

        if (model.CycleId is not Guid cycleId)
        {
            return;
        }

        Result<CycleResponse> cycle = await cycleQuery.Handle(new GetCycleByIdQuery(cycleId), cancellationToken);
        if (cycle.IsSuccess)
        {
            model.Cycle = cycle.Value;
            model.FarmerDebt = await FarmerDebtAsync(cycle.Value.FarmerId, cancellationToken);
        }
    }

    private async Task<FarmerPlasmaDebtResponse?> FarmerDebtAsync(Guid farmerId, CancellationToken cancellationToken)
    {
        Result<FarmerPlasmaDebtResponse> debt = await debtQuery.Handle(new GetFarmerPlasmaDebtQuery(farmerId), cancellationToken);
        return debt.IsSuccess ? debt.Value : null;
    }
}
