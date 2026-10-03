using Application.Abstractions.Messaging;
using Application.Finance.CashBank;
using Domain.Access;
using Domain.Finance.CashBank;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.Finance.Models;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Finance.Controllers;

/// <summary>
/// Cash and bank accounts per branch, each linked to its own postable asset account of the chart of accounts, and
/// their cash/bank book.
/// </summary>
[Area("Finance")]
[MenuAccess(MenuCodes.FinanceCashBankAccounts)]
public sealed class CashBankAccountsController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetCashBankAccountsQuery, IReadOnlyList<CashBankAccountResponse>> accountsQuery) : AppController
{
    private const string MenuCode = MenuCodes.FinanceCashBankAccounts;

    private static readonly ExportColumn<CashBankAccountResponse>[] Columns =
    [
        new("Code", a => a.Code, Width: 1),
        new("Name", a => a.Name, Width: 2.5f),
        new("Type", a => a.Type, Width: 0.7f),
        new("Branch", a => a.BranchCode, Width: 0.8f),
        new("Ledger account", a => $"{a.AccountCode} — {a.AccountName}", Width: 2.5f),
        new("Bank", a => a.BankName, Width: 1.2f),
        new("Account number", a => a.AccountNumber, Width: 1.5f),
        new("Balance", a => a.Balance, ExportFormat.Money, 1.3f),
        new("Active", a => a.IsActive, ExportFormat.Boolean, 0.6f)
    ];

    private static readonly ExportColumn<CashBankLedgerLine>[] LedgerColumns =
    [
        new("Date", l => l.Date, ExportFormat.Date, 1),
        new("Journal", l => l.Number, Width: 1.5f),
        new("Source", l => l.SourceType, Width: 1.2f),
        new("Description", l => l.Description, Width: 3),
        new("In", l => l.In, ExportFormat.Money, 1.3f),
        new("Out", l => l.Out, ExportFormat.Money, 1.3f),
        new("Balance", l => l.Balance, ExportFormat.Money, 1.4f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? branch, CashBankAccountType? type, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        IReadOnlyList<CashBankAccountResponse> accounts = await ListAsync(branchFilter.BranchId, type, search, cancellationToken);

        return View(new ListViewModel<CashBankAccountResponse>
        {
            Rows = new PagedList<CashBankAccountResponse>(accounts, 1, Math.Max(accounts.Count, 1), accounts.Count),
            Search = search,
            BranchOptions = branchFilter.Options,
            FilterOptions = new Dictionary<string, IReadOnlyList<SelectListItem>> { ["type"] = EnumOptions.For(type) }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(
        string? format, string? search, string? branch, CashBankAccountType? type, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        List<string> filters = [branchFilter.Description];
        if (type is not null)
        {
            filters.Add($"Type: {type}");
        }

        return await support.ExportAsync(format, "Cash/Bank Accounts", "cash-bank-accounts", filters, Columns,
            await ListAsync(branchFilter.BranchId, type, search, cancellationToken));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create() =>
        View("Form", new CashBankAccountFormViewModel
        {
            Type = CashBankAccountType.Bank,
            Branches = await support.BranchOptionsAsync(null)
        });

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        CashBankAccountFormViewModel model,
        [FromServices] ICommandHandler<CreateCashBankAccountCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateCashBankAccountCommand(
                    model.Code, model.Name, model.Type!.Value, model.BranchId!.Value, model.AccountId!.Value, model.BankName, model.AccountNumber),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Cash/bank account {model.Code} has been created.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        model.Branches = await support.BranchOptionsAsync(model.BranchId);
        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        CashBankAccountResponse? account = await FindAsync(id, cancellationToken);

        if (account is null)
        {
            return NotFound();
        }

        var model = CashBankAccountFormViewModel.From(account);
        model.CanSave = await support.CanAsync(MenuCode, MenuRights.Edit);

        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        CashBankAccountFormViewModel model,
        [FromServices] ICommandHandler<UpdateCashBankAccountCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateCashBankAccountCommand(id, model.Name, model.BankName, model.AccountNumber, model.IsActive == true),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Cash/bank account {model.Code} has been saved.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        // Read-only details (branch, ledger account, balance) come from the saved account.
        if (model.Id is Guid accountId && await FindAsync(accountId, cancellationToken) is { } saved)
        {
            var details = CashBankAccountFormViewModel.From(saved);
            model.BranchCode = details.BranchCode;
            model.AccountLabel = details.AccountLabel;
            model.Balance = details.Balance;
        }

        return View("Form", model);
    }

    /// <summary>
    /// Buku kas/bank: opening balance, posted movements with a running balance and the closing balance.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Ledger(
        Guid id,
        DateOnly? from,
        DateOnly? to,
        [FromServices] IQueryHandler<GetCashBankLedgerQuery, CashBankLedgerResponse> ledgerQuery,
        CancellationToken cancellationToken)
    {
        CashBankAccountResponse? account = await FindAsync(id, cancellationToken);

        if (account is null)
        {
            return NotFound();
        }

        DateOnly today = formatter.Today();
        DateOnly start = from ?? new DateOnly(today.Year, today.Month, 1);
        DateOnly end = to ?? today;
        Result<CashBankLedgerResponse> ledger = await ledgerQuery.Handle(new GetCashBankLedgerQuery(id, start, end), cancellationToken);

        if (ledger.IsFailure)
        {
            NotifyError(ledger.Error.Description);
        }

        return View(new CashBankLedgerViewModel { Account = account, From = start, To = end, Ledger = ledger.IsSuccess ? ledger.Value : null });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> LedgerExport(
        string? format,
        Guid id,
        DateOnly from,
        DateOnly to,
        [FromServices] IQueryHandler<GetCashBankLedgerQuery, CashBankLedgerResponse> ledgerQuery,
        CancellationToken cancellationToken)
    {
        Result<CashBankLedgerResponse> result = await ledgerQuery.Handle(new GetCashBankLedgerQuery(id, from, to), cancellationToken);

        if (result.IsFailure)
        {
            NotifyError(result.Error.Description);
            return RedirectToAction(nameof(Ledger), new { id, from, to });
        }

        CashBankLedgerResponse ledger = result.Value;
        return await support.ExportAsync(format, "Cash/Bank Book", $"cash-bank-book-{ledger.Code}",
            [
                $"Account: {ledger.Code} — {ledger.Name} ({ledger.AccountCode})",
                $"Period: {formatter.Date(from)} – {formatter.Date(to)}",
                $"Opening: Rp {formatter.Number(ledger.OpeningBalance)} · Closing: Rp {formatter.Number(ledger.ClosingBalance)}"
            ],
            LedgerColumns, ledger.Lines);
    }

    private async Task<CashBankAccountResponse?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        (await ListAsync(null, null, null, cancellationToken)).FirstOrDefault(a => a.Id == id);

    private async Task<IReadOnlyList<CashBankAccountResponse>> ListAsync(
        Guid? branchId, CashBankAccountType? type, string? search, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<CashBankAccountResponse>> result = await accountsQuery.Handle(
            new GetCashBankAccountsQuery(branchId, type, IncludeInactive: true), cancellationToken);

        if (result.IsFailure)
        {
            return [];
        }

        return string.IsNullOrWhiteSpace(search)
            ? result.Value
            : [.. result.Value.Where(a =>
                a.Code.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                (a.AccountNumber?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))];
    }
}
