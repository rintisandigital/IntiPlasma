using Application.Abstractions.Messaging;
using Application.TaxCodes.Get;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.MasterData.Models;

namespace Web.App.Areas.Sales;

/// <summary>
/// Options shared by the sales pages.
/// </summary>
public static class SalesLists
{
    /// <summary>
    /// Active VAT codes plus the inactive ones already chosen on the lines.
    /// </summary>
    public static async Task<IReadOnlyList<SelectListItem>> VatCodesAsync(
        IQueryHandler<GetTaxCodesQuery, IReadOnlyList<TaxCodeResponse>> query,
        IEnumerable<Guid?> chosen,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        Result<IReadOnlyList<TaxCodeResponse>> taxCodes = await query.Handle(new GetTaxCodesQuery(), cancellationToken);
        HashSet<Guid?> keep = [.. chosen];

        return taxCodes.IsSuccess
            ? [.. taxCodes.Value
                .Where(t => t.Type == "Vat" && (t.IsActive || keep.Contains(t.Id)))
                .Select(t => new SelectListItem($"{t.Code} — {t.Name}", t.Id.ToString()))]
            : [];
    }
}
