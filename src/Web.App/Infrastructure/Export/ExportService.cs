using System.Globalization;
using Application.Users.GetCurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Web.App.Infrastructure.Auth;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Infrastructure.Export;

/// <summary>
/// Builds export files for the screens: title block (company, active branch, printed by/at), Excel or PDF,
/// file name <c>{name}_{branch}_{yyyyMMdd-HHmm}.{ext}</c>, and an audit trail entry per export (PLAN-WEBAPP §5, §23.6).
/// </summary>
public sealed partial class ExportService(
    IOptions<AppOptions> appOptions,
    IOptions<ExportOptions> exportOptions,
    IBranchContext branchContext,
    IHttpContextAccessor httpContextAccessor,
    DisplayFormatter formatter,
    PdfListExporter pdfExporter,
    ReportExporter reportExporter,
    ILogger<ExportService> logger)
{
    public int MaxRows => exportOptions.Value.MaxRows;

    public async Task<ExportHeader> HeaderAsync(string title, string fileName, IReadOnlyList<string>? filters = null)
    {
        CurrentUserBranch? branch = await branchContext.GetActiveBranchAsync();

        return new ExportHeader(
            title,
            fileName,
            filters ?? [],
            appOptions.Value.CompanyName,
            branch?.Name,
            branch?.Code,
            httpContextAccessor.HttpContext?.User.GetDisplayName() ?? string.Empty,
            formatter.LocalNow());
    }

    /// <summary>
    /// A list export as a downloadable file.
    /// </summary>
    public FileContentResult List<T>(
        ExportFileFormat format,
        ExportHeader header,
        IReadOnlyList<ExportColumn<T>> columns,
        IReadOnlyList<T> rows)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(rows);

        byte[] content = format == ExportFileFormat.Excel
            ? ExcelExporter.Export(header, columns, rows)
            : pdfExporter.Export(header, columns, rows);

        Log(header, format, rows.Count, isDocument: false);

        return File(header, format, content);
    }

    /// <summary>
    /// A structured report (financial statements, ledgers, tax recap) as Excel or PDF.
    /// </summary>
    public FileContentResult Report(ExportFileFormat format, ExportHeader header, ReportDocument report)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(report);

        byte[] content = format == ExportFileFormat.Excel
            ? ReportExporter.Excel(header, report)
            : reportExporter.Pdf(header, report);

        Log(header, format, report.Tables.Sum(t => t.Rows.Count), isDocument: false);

        return File(header, format, content);
    }

    /// <summary>
    /// A CSV file for import elsewhere (RFC 4180, comma separated, invariant numbers, ISO dates, UTF-8 with BOM) —
    /// the same layout as the Web.Api tax exports.
    /// </summary>
    public FileContentResult Csv<T>(string title, string fileName, IEnumerable<T> rows, params (string Header, Func<T, object?> Value)[] columns)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);

        var builder = new System.Text.StringBuilder();
        builder.AppendLine(string.Join(',', columns.Select(c => CsvEscape(c.Header))));
        int count = 0;
        foreach (T row in rows)
        {
            builder.AppendLine(string.Join(',', columns.Select(c => CsvEscape(CsvFormat(c.Value(row))))));
            count++;
        }

        LogExport(logger, httpContextAccessor.HttpContext?.User.GetDisplayName() ?? string.Empty, title, "Csv", count, fileName);
        PendingExport.Track(httpContextAccessor.HttpContext, new PendingExport(title, "Csv", count, [fileName], null, IsDocument: false));

        byte[] content = [.. System.Text.Encoding.UTF8.GetPreamble(), .. System.Text.Encoding.UTF8.GetBytes(builder.ToString())];
        return new FileContentResult(content, "text/csv") { FileDownloadName = fileName };
    }

    private static string CsvFormat(object? value) => value switch
    {
        null => string.Empty,
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
        bool flag => flag ? "Y" : "N",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string CsvEscape(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;

    /// <summary>
    /// A printed document (PO, invoice, contract, …) on portrait A4 with the shared letterhead and footer.
    /// </summary>
    public FileContentResult Document(ExportHeader header, Action<IContainer> content)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(content);

        byte[] pdf = QuestPDF.Fluent.Document.Create(document => document.Page(page =>
        {
            PdfLayout.Page(page, header, landscape: false);
            page.Content().Element(content);
        })).GeneratePdf();

        Log(header, ExportFileFormat.Pdf, 1, isDocument: true);

        return File(header, ExportFileFormat.Pdf, pdf);
    }

    private static FileContentResult File(ExportHeader header, ExportFileFormat format, byte[] content)
    {
        string branch = string.IsNullOrEmpty(header.BranchCode) ? "ALL" : header.BranchCode;
        string stamp = header.PrintedAtLocal.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        string extension = format == ExportFileFormat.Excel ? "xlsx" : "pdf";

        return new FileContentResult(content, format == ExportFileFormat.Excel ? ExcelExporter.ContentType : PdfListExporter.ContentType)
        {
            FileDownloadName = $"{header.FileName}_{branch}_{stamp}.{extension}"
        };
    }

    private void Log(ExportHeader header, ExportFileFormat format, int rows, bool isDocument)
    {
        LogExport(logger, header.PrintedBy, header.Title, format.ToString(), rows, string.Join("; ", header.Filters));

        // Written to the audit trail by ExportAuditFilter once the file has been sent.
        PendingExport.Track(
            httpContextAccessor.HttpContext,
            new PendingExport(header.Title, format == ExportFileFormat.Pdf ? "PDF" : format.ToString(), rows, header.Filters, header.BranchName, isDocument));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Export by {User}: {Title} as {Format}, {Rows} rows, filters: {Filters}")]
    private static partial void LogExport(ILogger logger, string user, string title, string format, int rows, string filters);
}
