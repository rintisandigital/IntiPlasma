using Application.Abstractions.Messaging;
using Application.Finance.CashBank;
using Application.Finance.CostCenters;
using Application.TaxCodes.Get;
using Domain.Finance.CashBank;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;

namespace Web.App.Areas.Finance;

/// <summary>
/// Select options shared by the finance forms.
/// </summary>
public static class FinanceOptions
{
    /// <summary>
    /// Active cost centers plus the inactive ones already chosen (so an existing line keeps its value).
    /// </summary>
    public static async Task<IReadOnlyList<SelectListItem>> CostCentersAsync(
        IQueryHandler<GetCostCentersQuery, IReadOnlyList<CostCenterResponse>> query,
        IEnumerable<Guid?> chosen,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<CostCenterResponse>> result = await query.Handle(new GetCostCentersQuery(), cancellationToken);
        HashSet<Guid> keep = [.. chosen.OfType<Guid>()];

        return result.IsFailure
            ? []
            : [.. result.Value
                .Where(c => c.IsActive || keep.Contains(c.Id))
                .Select(c => new SelectListItem($"{c.Code} — {c.Name}", c.Id.ToString()))];
    }

    /// <summary>
    /// Active cash/bank accounts of the user's branches, labelled with their branch.
    /// </summary>
    public static async Task<IReadOnlyList<CashBankAccountResponse>> CashBankAccountsAsync(
        IQueryHandler<GetCashBankAccountsQuery, IReadOnlyList<CashBankAccountResponse>> query,
        CashBankAccountType? type,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<CashBankAccountResponse>> result = await query.Handle(new GetCashBankAccountsQuery(null, type, false), cancellationToken);

        return result.IsSuccess ? result.Value : [];
    }

    public static IReadOnlyList<SelectListItem> CashBankOptions(IEnumerable<CashBankAccountResponse> accounts, Guid? selected) =>
        [.. accounts.Select(a => new SelectListItem($"{a.Code} — {a.Name} ({a.BranchCode})", a.Id.ToString(), a.Id == selected))];

    /// <summary>
    /// Active income tax (PPh) codes plus the inactive one already chosen.
    /// </summary>
    public static async Task<IReadOnlyList<SelectListItem>> IncomeTaxCodesAsync(
        IQueryHandler<GetTaxCodesQuery, IReadOnlyList<TaxCodeResponse>> query,
        Guid? chosen,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<TaxCodeResponse>> taxCodes = await query.Handle(new GetTaxCodesQuery(), cancellationToken);

        return taxCodes.IsSuccess
            ? [.. taxCodes.Value
                .Where(t => t.Type == "IncomeTax" && (t.IsActive || t.Id == chosen))
                .Select(t => new SelectListItem($"{t.Code} — {t.Name}", t.Id.ToString(), t.Id == chosen))]
            : [];
    }
}
