namespace Web.App.Infrastructure.Export;

/// <summary>
/// A structured report (PLAN-WEBAPP §5.2–5.3): one or more tables whose rows are typed (section heading, detail,
/// subtotal, total) so Excel and PDF render the same structure — bold totals, indented accounts, one worksheet per
/// table in Excel.
/// </summary>
public sealed record ReportDocument(IReadOnlyList<ReportTable> Tables, bool Landscape = false);

/// <param name="Title">Table (and worksheet) title; omitted for a single-table report when null.</param>
/// <param name="Notes">Lines shown under the table (e.g. "balanced", totals in words).</param>
public sealed record ReportTable(
    string? Title,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<ReportRow> Rows,
    IReadOnlyList<string>? Notes = null);

public sealed record ReportColumn(string Header, ExportFormat Format = ExportFormat.Text, float Width = 2);

public enum ReportRowKind
{
    Detail,
    Section,
    Subtotal,
    Total
}

/// <param name="Indent">Indentation level of the first cell (accounts under a section).</param>
public sealed record ReportRow(ReportRowKind Kind, IReadOnlyList<object?> Cells, int Indent = 0)
{
    /// <summary>
    /// Link of the first cell on the screen (e.g. the journal of a ledger line); not exported.
    /// </summary>
    public string? Link { get; init; }

    public static ReportRow Detail(params object?[] cells) => new(ReportRowKind.Detail, cells);

    public static ReportRow Indented(int indent, params object?[] cells) => new(ReportRowKind.Detail, cells, indent);

    public static ReportRow Section(string title) => new(ReportRowKind.Section, [title]);

    public static ReportRow Subtotal(params object?[] cells) => new(ReportRowKind.Subtotal, cells);

    public static ReportRow Total(params object?[] cells) => new(ReportRowKind.Total, cells);
}
