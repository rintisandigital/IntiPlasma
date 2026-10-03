using System.Globalization;
using Application.Auditing;
using SharedKernel;

namespace Web.App.Areas.Admin.Models;

/// <summary>
/// Query string filter of the audit log list and its export.
/// </summary>
public sealed class AuditLogFilter
{
    public string? Search { get; set; }

    /// <summary>
    /// Access, Export or SignIn; empty for all.
    /// </summary>
    public string? Category { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public Guid? UserId { get; set; }

    public IReadOnlyList<string> Describe()
    {
        var filters = new List<string>();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            filters.Add($"Search: {Search}");
        }

        if (!string.IsNullOrWhiteSpace(Category))
        {
            filters.Add($"Category: {Category}");
        }

        if (From is not null || To is not null)
        {
            filters.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"Period: {From?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "…"} – {To?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "…"}"));
        }

        if (UserId is not null)
        {
            filters.Add($"User id: {UserId}");
        }

        return filters;
    }
}

public sealed record AuditLogIndexViewModel(PagedList<AuditLogListItem> Rows, AuditLogFilter Filter);
