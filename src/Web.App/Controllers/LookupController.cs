using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Farmers;
using Application.Finance.Accounts;
using Application.Farmers.Get;
using Application.Items;
using Application.Items.Get;
using Domain.Finance.Accounts;
using Domain.MasterData.Farmers;
using Domain.MasterData.Items;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Controllers;

/// <summary>
/// Search endpoints for Tom-Select dropdowns (<c>select[data-lookup]</c>, ~/js/lookup.js): id + label, at most
/// 20 matches. Results go through the same use cases as the lists, so branch scope applies.
/// </summary>
[AuthenticatedOnly]
public sealed class LookupController : AppController
{
    private const int MaxResults = 20;

    [HttpGet]
    public async Task<IActionResult> Items(
        string? q,
        ItemCategory? category,
        [FromServices] IQueryHandler<GetItemsQuery, PagedList<ItemResponse>> query,
        CancellationToken cancellationToken)
    {
        Result<PagedList<ItemResponse>> result = await query.Handle(
            new GetItemsQuery(new PageRequest(1, MaxResults, q), category), cancellationToken);

        return Json(result.IsFailure
            ? []
            : result.Value.Items.Where(i => i.IsActive).Select(i => new LookupItem(i.Id, $"{i.Code} — {i.Name} ({i.BaseUomCode})")));
    }

    [HttpGet]
    public async Task<IActionResult> Farmers(
        string? q,
        Guid? branchId,
        FarmerType? type,
        [FromServices] IQueryHandler<GetFarmersQuery, PagedList<FarmerResponse>> query,
        CancellationToken cancellationToken)
    {
        Result<PagedList<FarmerResponse>> result = await query.Handle(
            new GetFarmersQuery(new PageRequest(1, MaxResults, q), branchId, type), cancellationToken);

        return Json(result.IsFailure
            ? []
            : result.Value.Items.Where(f => f.IsActive).Select(f => new LookupItem(f.Id, $"{f.Code} — {f.Name} ({f.Type}, {f.BranchCode})")));
    }

    /// <summary>
    /// Postable, active accounts of the chart of accounts (journal lines, mappings, cash/bank accounts).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Accounts(
        string? q,
        AccountType? type,
        [FromServices] IQueryHandler<GetAccountsQuery, IReadOnlyList<AccountResponse>> query,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<AccountResponse>> result = await query.Handle(new GetAccountsQuery(type, q, PostableOnly: true), cancellationToken);

        return Json(result.IsFailure
            ? []
            : result.Value.Take(MaxResults).Select(a => new LookupItem(a.Id, $"{a.Code} — {a.Name}")));
    }

    public sealed record LookupItem(Guid Value, string Text);
}
