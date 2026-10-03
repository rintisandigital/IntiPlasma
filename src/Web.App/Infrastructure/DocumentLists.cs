using System.Globalization;
using Web.App.Areas.MasterData.Models;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Infrastructure;

/// <summary>
/// Query-string values and export filter descriptions shared by the document lists (date range, status, branch).
/// </summary>
public static class DocumentLists
{
    public static Dictionary<string, string?> DateFilters(DateOnly? from, DateOnly? to) => new()
    {
        ["from"] = from?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["to"] = to?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    };

    public static List<string> Filters(BranchFilter branchFilter, string? status, DateOnly? from, DateOnly? to, DisplayFormatter formatter)
    {
        ArgumentNullException.ThrowIfNull(branchFilter);
        ArgumentNullException.ThrowIfNull(formatter);

        List<string> filters = [branchFilter.Description];
        if (status is not null)
        {
            filters.Add($"Status: {EnumOptions.Label(status)}");
        }

        if (from is not null || to is not null)
        {
            filters.Add($"Date: {(from is { } f ? formatter.Date(f) : "…")} – {(to is { } t ? formatter.Date(t) : "…")}");
        }

        return filters;
    }
}
