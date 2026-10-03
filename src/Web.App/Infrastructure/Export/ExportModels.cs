namespace Web.App.Infrastructure.Export;

/// <summary>
/// How a column value is written: Excel keeps numbers and dates as real values (so they can be summed and
/// filtered); PDF formats them the Indonesian way (W-16).
/// </summary>
public enum ExportFormat
{
    Text,
    WholeNumber,
    Number,
    Money,
    Percent,
    Date,
    DateTime,
    Boolean
}

/// <summary>
/// One column of a list export, shared by Excel and PDF (PLAN-WEBAPP §5.1).
/// </summary>
/// <param name="Width">Relative width in the PDF table (Excel columns are auto-fitted).</param>
public sealed record ExportColumn<T>(string Header, Func<T, object?> Value, ExportFormat Format = ExportFormat.Text, float Width = 2);

public enum ExportFileFormat
{
    Excel,
    Pdf
}

public static class ExportFileFormats
{
    public static bool TryParse(string? value, out ExportFileFormat format)
    {
        switch (value?.ToUpperInvariant())
        {
            case "XLSX":
            case "EXCEL":
                format = ExportFileFormat.Excel;
                return true;
            case "PDF":
                format = ExportFileFormat.Pdf;
                return true;
            default:
                format = default;
                return false;
        }
    }
}

/// <summary>
/// Title block of an export: report name, the filters applied, and who/when/which branch.
/// </summary>
/// <param name="FileName">Base of the file name, e.g. "items" → items_BDG_20261003-1405.xlsx.</param>
public sealed record ExportHeader(
    string Title,
    string FileName,
    IReadOnlyList<string> Filters,
    string CompanyName,
    string? BranchName,
    string? BranchCode,
    string PrintedBy,
    DateTime PrintedAtLocal);

public sealed class ExportOptions
{
    public const string SectionName = "Export";

    /// <summary>
    /// Larger lists must be narrowed with filters (PLAN-WEBAPP §5.1).
    /// </summary>
    public int MaxRows { get; init; } = 50_000;
}
