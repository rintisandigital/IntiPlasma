using Application.Abstractions.Messaging;
using Application.Auditing;
using Microsoft.AspNetCore.Mvc.Filters;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Infrastructure.Export;

/// <summary>
/// An export or printed document produced by <see cref="ExportService"/> during the current request.
/// </summary>
public sealed record PendingExport(
    string Title,
    string Format,
    int Rows,
    IReadOnlyList<string> Filters,
    string? Branch,
    bool IsDocument)
{
    private const string ItemKey = "export-audit";

    public static void Track(HttpContext? context, PendingExport export)
    {
        context?.Items[ItemKey] = export;
    }

    public static PendingExport? Get(HttpContext context) => context.Items[ItemKey] as PendingExport;
}

/// <summary>
/// Records every export in the audit trail once the file has been sent (W10, category Export): menu of the
/// action, title, format, rows, filters, branch and — for printed documents — the document id.
/// </summary>
public sealed class ExportAuditFilter(ICommandHandler<RecordExportCommand> recordExport) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        ResultExecutedContext executed = await next();

        if (executed.Exception is not null || PendingExport.Get(context.HttpContext) is not { } export)
        {
            return;
        }

        string menu = context.ActionDescriptor.EndpointMetadata
            .OfType<MenuAccessAttribute>()
            .LastOrDefault()?.Code ?? context.HttpContext.Request.Path.Value ?? string.Empty;

        Guid? documentId = export.IsDocument && Guid.TryParse(context.RouteData.Values["id"]?.ToString(), out Guid id)
            ? id
            : null;

        await recordExport.Handle(
            new RecordExportCommand(menu, export.Title, export.Format, export.Rows, export.Filters, export.Branch, documentId),
            CancellationToken.None);
    }
}
