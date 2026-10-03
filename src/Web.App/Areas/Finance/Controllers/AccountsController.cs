using Application.Abstractions.Messaging;
using Application.Finance.Accounts;
using Domain.Access;
using Domain.Finance.Accounts;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Finance.Models;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;

namespace Web.App.Areas.Finance.Controllers;

/// <summary>
/// Chart of accounts: a tree of header accounts (grouping) and postable detail accounts. Code, type, parent,
/// postability and normal balance are fixed at creation; name, cash flow category and status can change.
/// </summary>
[Area("Finance")]
[MenuAccess(MenuCodes.FinanceAccounts)]
public sealed class AccountsController(
    PageSupport support,
    IQueryHandler<GetAccountsQuery, IReadOnlyList<AccountResponse>> accountsQuery) : AppController
{
    private const string MenuCode = MenuCodes.FinanceAccounts;

    private static readonly ExportColumn<AccountResponse>[] Columns =
    [
        new("Code", a => a.Code, Width: 1),
        new("Name", a => $"{new string(' ', (a.Level - 1) * 3)}{a.Name}", Width: 3.5f),
        new("Type", a => a.Type, Width: 1),
        new("Level", a => a.Level, ExportFormat.WholeNumber, 0.6f),
        new("Parent", a => a.ParentCode, Width: 1),
        new("Postable", a => a.IsPostable, ExportFormat.Boolean, 0.8f),
        new("Normal balance", a => a.NormalBalance, Width: 1),
        new("Cash flow", a => a.CashFlowCategory, Width: 1),
        new("Active", a => a.IsActive, ExportFormat.Boolean, 0.7f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, AccountType? type, string? status, CancellationToken cancellationToken) =>
        View(new AccountListViewModel
        {
            Accounts = await ListAsync(search, type, status, cancellationToken),
            Search = search,
            Type = type,
            Status = status,
            TypeOptions = EnumOptions.For(type)
        });

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, string? search, AccountType? type, string? status, CancellationToken cancellationToken)
    {
        List<string> filters = [];
        if (type is not null)
        {
            filters.Add($"Type: {type}");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            filters.Add($"Search: {search}");
        }

        return await support.ExportAsync(
            format, "Chart of Accounts", "chart-of-accounts", filters, Columns, await ListAsync(search, type, status, cancellationToken));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(Guid? parentId, CancellationToken cancellationToken)
    {
        var model = new AccountFormViewModel { ParentId = parentId, IsPostable = true };

        if (parentId is not null)
        {
            AccountResponse? parent = (await AllAsync(cancellationToken)).FirstOrDefault(a => a.Id == parentId);
            model.Type = parent is null ? null : Enum.Parse<AccountType>(parent.Type);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        AccountFormViewModel model,
        [FromServices] ICommandHandler<CreateAccountCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateAccountCommand(
                    model.Code, model.Name, model.Type!.Value, model.ParentId, model.IsPostable == true, model.NormalBalance,
                    model.CashFlowCategory),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Account {model.Code} has been created.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        IReadOnlyList<AccountResponse> accounts = await AllAsync(cancellationToken);
        AccountResponse? account = accounts.FirstOrDefault(a => a.Id == id);

        if (account is null)
        {
            return NotFound();
        }

        AccountResponse? parent = accounts.FirstOrDefault(a => a.Id == account.ParentId);

        return View("Form", new AccountFormViewModel
        {
            Id = account.Id,
            Code = account.Code,
            Name = account.Name,
            Type = Enum.Parse<AccountType>(account.Type),
            ParentId = account.ParentId,
            ParentLabel = parent is null ? null : $"{parent.Code} — {parent.Name}",
            IsPostable = account.IsPostable,
            NormalBalance = Enum.Parse<BalanceSide>(account.NormalBalance),
            CashFlowCategory = Enum.Parse<CashFlowCategory>(account.CashFlowCategory),
            IsActive = account.IsActive,
            CanSave = await support.CanAsync(MenuCode, MenuRights.Edit)
        });
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        AccountFormViewModel model,
        [FromServices] ICommandHandler<UpdateAccountCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateAccountCommand(id, model.Name, model.IsActive == true, model.CashFlowCategory), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Account {model.Code} has been saved.");
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Error);
        }

        AccountResponse? parent = (await AllAsync(cancellationToken)).FirstOrDefault(a => a.Id == model.ParentId);
        model.ParentLabel = parent is null ? null : $"{parent.Code} — {parent.Name}";

        return View("Form", model);
    }

    private async Task<IReadOnlyList<AccountResponse>> AllAsync(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<AccountResponse>> result = await accountsQuery.Handle(new GetAccountsQuery(null, null, false), cancellationToken);
        return result.IsSuccess ? result.Value : [];
    }

    private async Task<IReadOnlyList<AccountResponse>> ListAsync(
        string? search, AccountType? type, string? status, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<AccountResponse>> result = await accountsQuery.Handle(new GetAccountsQuery(type, search, false), cancellationToken);

        if (result.IsFailure)
        {
            return [];
        }

        return status switch
        {
            "active" => [.. result.Value.Where(a => a.IsActive)],
            "inactive" => [.. result.Value.Where(a => !a.IsActive)],
            _ => result.Value
        };
    }

    private async Task<AccountFormViewModel> WithOptionsAsync(AccountFormViewModel model, CancellationToken cancellationToken)
    {
        model.HeaderAccounts =
        [
            .. (await AllAsync(cancellationToken))
                .Where(a => !a.IsPostable && (a.IsActive || a.Id == model.ParentId))
                .Select(a => new HeaderAccountOption(a.Id, $"{new string('·', (a.Level - 1) * 2)} {a.Code} — {a.Name}".Trim(), a.Type))
        ];

        return model;
    }
}
