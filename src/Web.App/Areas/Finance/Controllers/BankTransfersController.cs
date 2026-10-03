using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Finance.CashBank;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
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
/// Transfers between two cash/bank accounts of one branch (e.g. petty cash replenishment), posted immediately.
/// </summary>
[Area("Finance")]
[MenuAccess(MenuCodes.FinanceBankTransfers)]
public sealed class BankTransfersController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetBankTransfersQuery, PagedList<BankTransferResponse>> transfersQuery,
    IQueryHandler<GetCashBankAccountsQuery, IReadOnlyList<CashBankAccountResponse>> cashBankQuery) : AppController
{
    private const string MenuCode = MenuCodes.FinanceBankTransfers;

    private static readonly ExportColumn<BankTransferResponse>[] Columns =
    [
        new("Number", t => t.Number, Width: 1.5f),
        new("Date", t => t.Date, ExportFormat.Date, 1),
        new("Branch", t => t.BranchCode, Width: 0.7f),
        new("From", t => $"{t.FromCode} — {t.FromName}", Width: 2),
        new("To", t => $"{t.ToCode} — {t.ToName}", Width: 2),
        new("Reference", t => t.Reference, Width: 1.2f),
        new("Notes", t => t.Notes, Width: 2),
        new("Amount", t => t.Amount, ExportFormat.Money, 1.3f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? branch, DateOnly? from, DateOnly? to, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<BankTransferResponse>> result = await transfersQuery.Handle(
            new GetBankTransfersQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, null, from, to),
            cancellationToken);

        return View(new ListViewModel<BankTransferResponse>
        {
            Rows = result.Value,
            Search = search,
            BranchOptions = branchFilter.Options,
            Filters = DocumentLists.DateFilters(from, to)
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(
        string? format, string? search, string? branch, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await support.ExportAsync(format, "Bank Transfers", "bank-transfers",
            DocumentLists.Filters(branchFilter, null, from, to, formatter), Columns,
            (paging, ct) => transfersQuery.Handle(new GetBankTransfersQuery(paging, branchFilter.BranchId, null, from, to), ct),
            search, cancellationToken);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(Guid? fromId, CancellationToken cancellationToken)
    {
        var model = new BankTransferFormViewModel { FromCashBankAccountId = fromId, Date = formatter.Today() };
        await PrepareAsync(model, cancellationToken);

        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        BankTransferFormViewModel model,
        [FromServices] ICommandHandler<CreateBankTransferCommand, CreateBankTransferResponse> handler,
        CancellationToken cancellationToken)
    {
        if (model.FromCashBankAccountId is not null && model.FromCashBankAccountId == model.ToCashBankAccountId)
        {
            ModelState.AddModelError(nameof(model.ToCashBankAccountId), "Choose a different account to transfer to.");
        }

        if (ModelState.IsValid)
        {
            Result<CreateBankTransferResponse> result = await handler.Handle(
                new CreateBankTransferCommand(
                    model.FromCashBankAccountId!.Value, model.ToCashBankAccountId!.Value, model.Date!.Value, model.Amount!.Value, model.Reference, model.Notes),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Transfer {result.Value.Number} of {formatter.Money(model.Amount!.Value)} has been posted.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        await PrepareAsync(model, cancellationToken);
        return View("Form", model);
    }

    private async Task PrepareAsync(BankTransferFormViewModel model, CancellationToken cancellationToken)
    {
        IReadOnlyList<CashBankAccountResponse> accounts = await FinanceOptions.CashBankAccountsAsync(cashBankQuery, null, cancellationToken);
        model.CashBankAccounts = FinanceOptions.CashBankOptions(accounts, null);
    }
}
