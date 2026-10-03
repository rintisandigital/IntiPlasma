using System.Globalization;
using Application.Abstractions.Messaging;
using Application.Finance.CashBank;
using Domain.Access;
using Domain.Finance.CashBank;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Finance.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;
using Web.App.Infrastructure.Workflow;

namespace Web.App.Areas.Finance.Controllers;

/// <summary>
/// Bank reconciliation: the bank statement (entered or imported from CSV) on one side, the uncleared ledger lines
/// of the bank's account on the other; lines are matched automatically (same amount, ±3 days) or by hand. A
/// reconciliation is completed when every statement line is matched and the balances agree.
/// </summary>
[Area("Finance")]
[MenuAccess(MenuCodes.FinanceBankReconciliations)]
public sealed class BankReconciliationsController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetBankReconciliationsQuery, IReadOnlyList<BankReconciliationSummary>> reconciliationsQuery,
    IQueryHandler<GetBankReconciliationByIdQuery, BankReconciliationResponse> reconciliationQuery,
    IQueryHandler<GetCashBankAccountsQuery, IReadOnlyList<CashBankAccountResponse>> cashBankQuery) : AppController
{
    private const string MenuCode = MenuCodes.FinanceBankReconciliations;
    private const string DocumentType = "BankReconciliation";

    /// <summary>
    /// Largest statement file accepted (the use case accepts up to 2 million characters).
    /// </summary>
    private const long MaxCsvBytes = 2_000_000;

    private static readonly ExportColumn<BankReconciliationSummary>[] Columns =
    [
        new("Bank account", r => r.CashBankCode, Width: 1.3f),
        new("Statement date", r => r.StatementDate, ExportFormat.Date, 1.1f),
        new("Statement balance", r => r.StatementBalance, ExportFormat.Money, 1.4f),
        new("Status", r => r.Status, Width: 1),
        new("Completed at", r => r.CompletedAtUtc, ExportFormat.DateTime, 1.3f)
    ];

    private static readonly ExportColumn<StatementLineResponse>[] StatementColumns =
    [
        new("#", l => l.LineNumber, ExportFormat.WholeNumber, 0.4f),
        new("Date", l => l.Date, ExportFormat.Date, 1),
        new("Description", l => l.Description, Width: 3),
        new("Amount", l => l.Amount, ExportFormat.Money, 1.3f),
        new("Matched journal", l => l.MatchedJournalNumber ?? "(unmatched)", Width: 1.6f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? branch, Guid? cashBankAccountId, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        IReadOnlyList<CashBankAccountResponse> accounts = await FinanceOptions.CashBankAccountsAsync(cashBankQuery, null, cancellationToken);

        return View(new BankReconciliationListViewModel
        {
            Rows = await ListAsync(branchFilter.BranchId, cashBankAccountId, cancellationToken),
            Branch = branch,
            CashBankAccountId = cashBankAccountId,
            BranchOptions = branchFilter.Options,
            CashBankAccounts = FinanceOptions.CashBankOptions(accounts, cashBankAccountId)
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, string? branch, Guid? cashBankAccountId, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await support.ExportAsync(format, "Bank Reconciliations", "bank-reconciliations", [branchFilter.Description], Columns,
            await ListAsync(branchFilter.BranchId, cashBankAccountId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<BankReconciliationResponse> result = await reconciliationQuery.Handle(new GetBankReconciliationByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        return View(new BankReconciliationDetailsViewModel(result.Value, await support.CanAsync(MenuCode, MenuRights.Edit)));
    }

    /// <summary>
    /// The statement lines with their matched journal, as Excel/PDF.
    /// </summary>
    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> StatementExport(string? format, Guid id, CancellationToken cancellationToken)
    {
        Result<BankReconciliationResponse> result = await reconciliationQuery.Handle(new GetBankReconciliationByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        BankReconciliationResponse reconciliation = result.Value;
        return await support.ExportAsync(format, "Bank Reconciliation", $"bank-reconciliation-{reconciliation.CashBankCode}",
            [
                $"Account: {reconciliation.CashBankCode} — {reconciliation.CashBankName}",
                $"Statement date: {formatter.Date(reconciliation.StatementDate)} · Status: {reconciliation.Status}",
                $"Statement: Rp {formatter.Number(reconciliation.StatementBalance)} · Book: Rp {formatter.Number(reconciliation.BookBalance)}"
                    + $" · Uncleared: Rp {formatter.Number(reconciliation.UnclearedNet)} · Difference: Rp {formatter.Number(reconciliation.Difference)}"
            ],
            StatementColumns, reconciliation.StatementLines);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(Guid? cashBankAccountId, CancellationToken cancellationToken)
    {
        var model = new BankReconciliationFormViewModel { CashBankAccountId = cashBankAccountId, StatementDate = formatter.Today() };
        await PrepareAsync(model, cancellationToken);

        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        BankReconciliationFormViewModel model,
        [FromServices] ICommandHandler<StartBankReconciliationCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new StartBankReconciliationCommand(model.CashBankAccountId!.Value, model.StatementDate!.Value, model.StatementBalance!.Value),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess("The reconciliation has been started; import or enter the statement lines.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }

            AddErrors(result.Error);
        }

        await PrepareAsync(model, cancellationToken);
        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> StatementBalance(
        Guid id,
        decimal? statementBalance,
        [FromServices] ICommandHandler<UpdateStatementBalanceCommand> handler,
        CancellationToken cancellationToken)
    {
        if (statementBalance is null)
        {
            NotifyError("Enter the statement closing balance.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await handler.Handle(new UpdateStatementBalanceCommand(id, statementBalance.Value), cancellationToken);
        return AfterAction(id, result, "The statement balance has been updated.");
    }

    /// <summary>
    /// Imports a CSV statement (<c>date,description,debit,credit</c>, bank's point of view), uploaded or pasted.
    /// </summary>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [RequestSizeLimit(MaxCsvBytes + 100_000)]
    public async Task<IActionResult> Import(
        Guid id,
        IFormFile? file,
        string? csv,
        [FromServices] ICommandHandler<ImportStatementLinesCommand, int> handler,
        CancellationToken cancellationToken)
    {
        string content = csv ?? string.Empty;
        if (file is { Length: > 0 })
        {
            if (file.Length > MaxCsvBytes)
            {
                NotifyError("The statement file is too large (max 2 MB).");
                return RedirectToAction(nameof(Details), new { id });
            }

            using var reader = new StreamReader(file.OpenReadStream());
            content = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            NotifyError("Choose a CSV file or paste the statement lines.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result<int> result = await handler.Handle(new ImportStatementLinesCommand(id, content), cancellationToken);

        return AfterAction(id, result.IsSuccess ? Result.Success() : Result.Failure(result.Error),
            result.IsSuccess ? $"{result.Value.ToString(CultureInfo.InvariantCulture)} statement line(s) imported." : string.Empty);
    }

    /// <param name="moneyIn">Credit on the statement (money in).</param>
    /// <param name="moneyOut">Debit on the statement (money out).</param>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> AddLine(
        Guid id,
        DateOnly? date,
        string? description,
        decimal? moneyIn,
        decimal? moneyOut,
        [FromServices] ICommandHandler<AddStatementLinesCommand> handler,
        CancellationToken cancellationToken)
    {
        decimal amount = (moneyIn ?? 0) - (moneyOut ?? 0);
        if (date is null || string.IsNullOrWhiteSpace(description) || amount == 0)
        {
            NotifyError("Enter the date, description and either money in or money out.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await handler.Handle(
            new AddStatementLinesCommand(id, [new StatementLineRequest(date.Value, description, amount)]), cancellationToken);
        return AfterAction(id, result, "The statement line has been added.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> RemoveLine(
        Guid id,
        int lineNumber,
        [FromServices] ICommandHandler<RemoveStatementLineCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new RemoveStatementLineCommand(id, lineNumber), cancellationToken);
        return AfterAction(id, result, "The statement line has been removed.");
    }

    /// <param name="entry">The ledger line chosen for the statement line, as "journalEntryId:lineNumber".</param>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Match(
        Guid id,
        int lineNumber,
        string? entry,
        [FromServices] ICommandHandler<MatchStatementLineCommand> handler,
        CancellationToken cancellationToken)
    {
        string[] parts = (entry ?? string.Empty).Split(':');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out Guid journalEntryId)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int journalLineNumber))
        {
            NotifyError("Choose the ledger line to match.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await handler.Handle(new MatchStatementLineCommand(id, lineNumber, journalEntryId, journalLineNumber), cancellationToken);
        return AfterAction(id, result, "The statement line has been matched.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Unmatch(
        Guid id,
        int lineNumber,
        [FromServices] ICommandHandler<UnmatchStatementLineCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new UnmatchStatementLineCommand(id, lineNumber), cancellationToken);
        return AfterAction(id, result, "The match has been removed.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> AutoMatch(
        Guid id,
        [FromServices] ICommandHandler<AutoMatchStatementLinesCommand, int> handler,
        CancellationToken cancellationToken)
    {
        Result<int> result = await handler.Handle(new AutoMatchStatementLinesCommand(id), cancellationToken);

        return AfterAction(id, result.IsSuccess ? Result.Success() : Result.Failure(result.Error),
            result.IsSuccess ? $"{result.Value.ToString(CultureInfo.InvariantCulture)} statement line(s) matched automatically." : string.Empty);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("complete")]
    public async Task<IActionResult> Complete(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<CompleteBankReconciliationCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "complete"),
            ct => handler.Handle(new CompleteBankReconciliationCommand(id), ct),
            cancellationToken);

        return AfterAction(id, result, "The reconciliation has been completed; its matched ledger lines are now cleared.");
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

    private async Task<IReadOnlyList<BankReconciliationSummary>> ListAsync(Guid? branchId, Guid? cashBankAccountId, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<BankReconciliationSummary>> result = await reconciliationsQuery.Handle(
            new GetBankReconciliationsQuery(cashBankAccountId, branchId), cancellationToken);

        return result.IsSuccess ? result.Value : [];
    }

    private async Task PrepareAsync(BankReconciliationFormViewModel model, CancellationToken cancellationToken)
    {
        IReadOnlyList<CashBankAccountResponse> accounts = await FinanceOptions.CashBankAccountsAsync(cashBankQuery, CashBankAccountType.Bank, cancellationToken);
        model.CashBankAccounts = FinanceOptions.CashBankOptions(accounts, null);
    }
}
