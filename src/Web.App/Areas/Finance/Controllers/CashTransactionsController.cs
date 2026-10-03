using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Documents;
using Application.Finance.CashBank;
using Application.Finance.CostCenters;
using Domain.Access;
using Domain.Documents.Attachments;
using Domain.Finance.CashBank;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.Finance.Documents;
using Web.App.Areas.Finance.Models;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;
using Web.App.Infrastructure.Workflow;

namespace Web.App.Areas.Finance.Controllers;

/// <summary>
/// Kas masuk / kas keluar against counter accounts (e.g. interest income, electricity): Draft → (cash-out only:
/// Approved by someone else) → Posted (number BKM/BKK, journaled). Draft/Approved can be cancelled.
/// </summary>
[Area("Finance")]
[MenuAccess(MenuCodes.FinanceCashTransactions)]
public sealed class CashTransactionsController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetCashTransactionsQuery, PagedList<CashTransactionResponse>> transactionsQuery,
    IQueryHandler<GetCashTransactionByIdQuery, CashTransactionResponse> transactionQuery,
    IQueryHandler<GetCashBankAccountsQuery, IReadOnlyList<CashBankAccountResponse>> cashBankQuery,
    IQueryHandler<GetCostCentersQuery, IReadOnlyList<CostCenterResponse>> costCentersQuery) : AppController
{
    private const string MenuCode = MenuCodes.FinanceCashTransactions;
    private const string DocumentType = "CashTransaction";

    private static readonly ExportColumn<CashTransactionResponse>[] Columns =
    [
        new("Number", t => t.Number ?? "(draft)", Width: 1.5f),
        new("Date", t => t.Date, ExportFormat.Date, 1),
        new("Branch", t => t.BranchCode, Width: 0.7f),
        new("Direction", t => t.Direction == nameof(CashDirection.In) ? "Cash in" : "Cash out", Width: 0.8f),
        new("Cash/bank", t => t.CashBankCode, Width: 1.1f),
        new("Description", t => t.Description, Width: 2.8f),
        new("Reference", t => t.Reference, Width: 1.2f),
        new("Amount", t => t.Amount, ExportFormat.Money, 1.3f),
        new("Status", t => t.Status, Width: 0.9f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? branch, CashTransactionStatus? status, CashDirection? direction, DateOnly? from, DateOnly? to, int? page,
        CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<CashTransactionResponse>> result = await transactionsQuery.Handle(
            new GetCashTransactionsQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, null, direction, status, from, to),
            cancellationToken);

        return View(new ListViewModel<CashTransactionResponse>
        {
            Rows = result.Value,
            Search = search,
            BranchOptions = branchFilter.Options,
            Filters = DocumentLists.DateFilters(from, to),
            FilterOptions = new Dictionary<string, IReadOnlyList<SelectListItem>>
            {
                ["status"] = EnumOptions.For(status),
                ["direction"] = DirectionOptions(direction)
            }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(
        string? format, string? search, string? branch, CashTransactionStatus? status, CashDirection? direction, DateOnly? from, DateOnly? to,
        CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        List<string> filters = DocumentLists.Filters(branchFilter, status?.ToString(), from, to, formatter);
        if (direction is not null)
        {
            filters.Add(direction == CashDirection.In ? "Cash in" : "Cash out");
        }

        return await support.ExportAsync(format, "Cash In/Out", "cash-transactions", filters, Columns,
            (paging, ct) => transactionsQuery.Handle(new GetCashTransactionsQuery(paging, branchFilter.BranchId, null, direction, status, from, to), ct),
            search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<CashTransactionResponse> result = await transactionQuery.Handle(new GetCashTransactionByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        return View(new CashTransactionDetailsViewModel(
            result.Value,
            await support.AttachmentsAsync(result.Value.Documents, cancellationToken),
            CanEdit: await support.CanAsync(MenuCode, MenuRights.Edit),
            CanPrint: await support.CanAsync(MenuCode, MenuRights.Export)));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(CashDirection? direction, CancellationToken cancellationToken)
    {
        var model = new CashTransactionFormViewModel
        {
            Direction = direction ?? CashDirection.Out,
            Date = formatter.Today(),
            Lines = [new CashLineInput()]
        };
        await PrepareAsync(model, cancellationToken);

        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        CashTransactionFormViewModel model,
        [FromServices] ICommandHandler<CreateCashTransactionCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        CashTransactionLineRequest[] lines = model.ToLines();
        if (lines.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one line.");
        }
        else if (Array.Exists(lines, l => l.AccountId == Guid.Empty || l.Amount <= 0))
        {
            ModelState.AddModelError(string.Empty, "Every line needs an account and an amount above zero.");
        }

        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateCashTransactionCommand(
                    model.CashBankAccountId!.Value, model.Direction!.Value, model.Date!.Value, model.Description, model.Reference, lines, model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess(model.IsIn
                    ? "The cash-in draft has been recorded; post it to journal it."
                    : "The cash-out draft has been recorded; another user must approve it before it is posted.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }

            AddErrors(result.Error);
        }

        await PrepareAsync(model, cancellationToken);
        return View("Form", model);
    }

    /// <summary>
    /// Checker approval of a cash-out: the domain refuses the maker approving their own transaction.
    /// </summary>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<ApproveCashTransactionCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "approve"),
            ct => handler.Handle(new ApproveCashTransactionCommand(id), ct),
            cancellationToken);

        return AfterAction(id, result, "The cash-out has been approved; it can now be posted.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("post")]
    public async Task<IActionResult> Post(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<PostCashTransactionCommand, string> handler,
        CancellationToken cancellationToken)
    {
        string? number = null;
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "post"),
            async ct =>
            {
                Result<string> posted = await handler.Handle(new PostCashTransactionCommand(id), ct);
                number = posted.IsSuccess ? posted.Value : null;
                return posted.IsSuccess ? Result.Success() : Result.Failure(posted.Error);
            },
            cancellationToken);

        return AfterAction(id, result, $"{number} has been posted and journaled.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        string? reason,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<CancelCashTransactionCommand> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            NotifyError("Enter the reason for cancelling.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "cancel"),
            ct => handler.Handle(new CancelCashTransactionCommand(id, reason), ct),
            cancellationToken);

        return AfterAction(id, result, "The transaction has been cancelled.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Documents(
        Guid id,
        List<Guid> documents,
        [FromServices] ICommandHandler<SetDocumentsCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new SetDocumentsCommand(AttachmentOwnerTypes.CashTransaction, id, documents), cancellationToken);
        return AfterAction(id, result, "The attachments have been saved.");
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Print(Guid id, CancellationToken cancellationToken)
    {
        Result<CashTransactionResponse> result = await transactionQuery.Handle(new GetCashTransactionByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        CashTransactionResponse transaction = result.Value;
        string title = transaction.Direction == nameof(CashDirection.In) ? "Cash Receipt Voucher" : "Cash Payment Voucher";
        if (transaction.Number is null)
        {
            title += " (DRAFT)";
        }

        string number = transaction.Number ?? "DRAFT";
        ExportHeader header = await support.Exports.HeaderAsync(title, $"cash-{number}", [$"No. {number}"]);

        return support.Exports.Document(header, container => FinanceDocumentPdf.CashTransaction(container, transaction, formatter));
    }

    private static IReadOnlyList<SelectListItem> DirectionOptions(CashDirection? selected) =>
    [
        new("Cash in", nameof(CashDirection.In), selected == CashDirection.In),
        new("Cash out", nameof(CashDirection.Out), selected == CashDirection.Out)
    ];

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

    private async Task PrepareAsync(CashTransactionFormViewModel model, CancellationToken cancellationToken)
    {
        IReadOnlyList<CashBankAccountResponse> accounts = await FinanceOptions.CashBankAccountsAsync(cashBankQuery, null, cancellationToken);
        model.CashBankAccounts = FinanceOptions.CashBankOptions(accounts, model.CashBankAccountId);
        model.CostCenters = await FinanceOptions.CostCentersAsync(costCentersQuery, model.Lines.Select(l => l.CostCenterId), cancellationToken);
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);
    }
}
