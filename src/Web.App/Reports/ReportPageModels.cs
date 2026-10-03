using Microsoft.AspNetCore.Mvc.Rendering;
using Web.App.Infrastructure.Export;

namespace Web.App.Reports;

public enum ReportFieldKind
{
    Date,
    Number,
    Select,
    Lookup,
    Checkbox
}

/// <summary>
/// One parameter of a report's filter form (submitted as a query string).
/// </summary>
/// <param name="Value">Current value as posted (invariant format), or the label of a lookup's chosen item.</param>
/// <param name="LookupUrl">Tom-Select endpoint of a <see cref="ReportFieldKind.Lookup"/> field.</param>
public sealed record ReportField(
    string Name,
    string Label,
    ReportFieldKind Kind,
    string? Value = null,
    IReadOnlyList<SelectListItem>? Options = null,
    string? LookupUrl = null,
    string? LookupLabel = null,
    bool Required = false,
    int Width = 2);

/// <param name="Report">Null until the parameters are complete (or when the query failed: see <see cref="Error"/>).</param>
/// <param name="ExportAction">Action that exports the report with the same query string (format=xlsx|pdf).</param>
/// <param name="CsvLinks">Extra downloads (label → URL), e.g. the tax CSV files.</param>
public sealed record ReportPageViewModel(
    string Title,
    string Subtitle,
    string MenuCode,
    IReadOnlyList<ReportField> Fields,
    ReportDocument? Report,
    string? Error,
    string ExportAction,
    IReadOnlyList<(string Label, string Url)>? CsvLinks = null);
