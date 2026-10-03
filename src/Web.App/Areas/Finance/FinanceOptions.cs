using Application.Abstractions.Messaging;
using Application.Finance.CostCenters;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;

namespace Web.App.Areas.Finance;

/// <summary>
/// Select options shared by the finance setup forms.
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
}
