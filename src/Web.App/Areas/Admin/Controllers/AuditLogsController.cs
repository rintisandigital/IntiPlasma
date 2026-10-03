using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Auditing;
using Domain.Access;
using Domain.Auditing;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Admin.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Admin.Controllers;

/// <summary>
/// Audit trail (W10, PLAN-WEBAPP §23.6): access changes, exports and sign-ins from Web.App and Web.Api.
/// Read-only; entries cannot be changed or deleted.
/// </summary>
[Area("Admin")]
[MenuAccess(MenuCodes.AdminAuditLogs)]
public sealed class AuditLogsController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetAuditLogsQuery, PagedList<AuditLogListItem>> logsQuery) : AppController
{
    private const string MenuCode = MenuCodes.AdminAuditLogs;

    [HttpGet]
    public async Task<IActionResult> Index(AuditLogFilter filter, int? page, CancellationToken cancellationToken)
    {
        Result<PagedList<AuditLogListItem>> result = await logsQuery.Handle(
            Query(filter, new PageRequest(page, PageRequest.DefaultPageSize, filter.Search)), cancellationToken);

        return View(new AuditLogIndexViewModel(result.Value, filter));
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        [FromServices] IQueryHandler<GetAuditLogByIdQuery, AuditLogListItem> query,
        CancellationToken cancellationToken)
    {
        Result<AuditLogListItem> result = await query.Handle(new GetAuditLogByIdQuery(id), cancellationToken);

        return result.IsSuccess ? View(result.Value) : NotFound();
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, AuditLogFilter filter, CancellationToken cancellationToken)
    {
        ExportColumn<AuditLogListItem>[] columns =
        [
            new("Time", a => formatter.ToLocal(a.OccurredAtUtc), ExportFormat.DateTime, 2),
            new("Category", a => a.Category, Width: 1),
            new("Action", a => a.Action, Width: 2),
            new("User", a => a.UserEmail, Width: 3),
            new("Summary", a => a.Summary, Width: 5),
            new("Record", a => a.EntityType, Width: 2),
            new("IP address", a => a.IpAddress, Width: 2),
            new("Application", a => a.Source, Width: 1)
        ];

        return await support.ExportAsync(
            format,
            "Audit Log",
            "audit-log",
            filter.Describe(),
            columns,
            (paging, ct) => logsQuery.Handle(Query(filter, paging), ct),
            filter.Search,
            cancellationToken);
    }

    private GetAuditLogsQuery Query(AuditLogFilter filter, PageRequest paging) => new(
        paging,
        filter.From is { } from ? formatter.StartOfDayUtc(from) : null,
        filter.To is { } to ? formatter.StartOfDayUtc(to.AddDays(1)) : null,
        Enum.TryParse(filter.Category, out AuditCategory category) ? category : null,
        filter.UserId);
}
