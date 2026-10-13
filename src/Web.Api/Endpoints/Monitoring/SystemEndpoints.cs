using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Auditing;
using Application.Monitoring;
using Domain.Auditing;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Monitoring;

/// <summary>
/// The Admin Office dashboard and the audit log.
/// </summary>
internal sealed class SystemEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // asOf is the client's local date; the month figures are those of its month.
        app.MapGet("dashboard", async (
            DateOnly asOf,
            Guid? branchId,
            IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<DashboardSummaryResponse> result = await handler.Handle(new GetDashboardSummaryQuery(asOf, branchId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Dashboard)
        .HasPermission(Permissions.DashboardRead);

        RouteGroupBuilder auditLogs = app.MapGroup("audit-logs").WithTags(Tags.AuditLogs);

        auditLogs.MapGet("", async (
            int? page,
            int? pageSize,
            DateTime? fromUtc,
            DateTime? toUtc,
            AuditCategory? category,
            Guid? userId,
            IQueryHandler<GetAuditLogsQuery, PagedList<AuditLogListItem>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PagedList<AuditLogListItem>> result = await handler.Handle(
                new GetAuditLogsQuery(new PageRequest(page, pageSize, null), fromUtc, toUtc, category, userId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.AuditLogsRead);

        auditLogs.MapGet("{auditLogId:guid}", async (
            Guid auditLogId,
            IQueryHandler<GetAuditLogByIdQuery, AuditLogListItem> handler,
            CancellationToken cancellationToken) =>
        {
            Result<AuditLogListItem> result = await handler.Handle(new GetAuditLogByIdQuery(auditLogId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.AuditLogsRead);
    }
}
