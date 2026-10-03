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
/// file name <c>{name}_{branch}_{yyyyMMdd-HHmm}.{ext}</c>, and an audit log line per export (PLAN-WEBAPP §5).
/// </summary>
public sealed partial class ExportService(
    IOptions<AppOptions> appOptions,
    IOptions<ExportOptions> exportOptions,
    IBranchContext branchContext,
    IHttpContextAccessor httpContextAccessor,
    DisplayFormatter formatter,
    PdfListExporter pdfExporter,
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

        Log(header, format, rows.Count);

        return File(header, format, content);
    }

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

        Log(header, ExportFileFormat.Pdf, 1);

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

    private void Log(ExportHeader header, ExportFileFormat format, int rows) =>
        LogExport(logger, header.PrintedBy, header.Title, format, rows, string.Join("; ", header.Filters));

    [LoggerMessage(Level = LogLevel.Information, Message = "Export by {User}: {Title} as {Format}, {Rows} rows, filters: {Filters}")]
    private static partial void LogExport(ILogger logger, string user, string title, ExportFileFormat format, int rows, string filters);
}
