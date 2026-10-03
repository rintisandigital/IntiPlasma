using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Dapper;
using Domain.Auditing;
using SharedKernel;

namespace Application.Auditing;

/// <summary>
/// Audit trail, newest first. Search covers summary, action, user email and entity type; the period is
/// [<paramref name="FromUtc"/>, <paramref name="ToUtc"/>) so the caller decides the time zone of a "day".
/// </summary>
public sealed record GetAuditLogsQuery(
    PageRequest Paging,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    AuditCategory? Category = null,
    Guid? UserId = null) : IQuery<PagedList<AuditLogListItem>>;

public sealed record GetAuditLogByIdQuery(Guid AuditLogId) : IQuery<AuditLogListItem>;

public sealed record AuditLogListItem
{
    public Guid Id { get; init; }

    public DateTime OccurredAtUtc { get; init; }

    public string Category { get; init; }

    public string Action { get; init; }

    public Guid? UserId { get; init; }

    public string? UserEmail { get; init; }

    public string? EntityType { get; init; }

    public Guid? EntityId { get; init; }

    public string Summary { get; init; }

    /// <summary>
    /// JSON of the change or of the export filters.
    /// </summary>
    public string? Details { get; init; }

    public string? IpAddress { get; init; }

    public string Source { get; init; }
}

public static class AuditLogErrors
{
    public static Error NotFound(Guid id) => Error.NotFound(
        "AuditLogs.NotFound",
        $"The audit log entry with the Id = '{id}' was not found");
}

internal sealed class GetAuditLogsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetAuditLogsQuery, PagedList<AuditLogListItem>>
{
    private const string Filter =
        """
        WHERE (@Search IS NULL OR a.summary ILIKE @Search OR a.action ILIKE @Search
               OR a.user_email ILIKE @Search OR a.entity_type ILIKE @Search)
          AND (CAST(@FromUtc AS timestamptz) IS NULL OR a.occurred_at_utc >= @FromUtc)
          AND (CAST(@ToUtc AS timestamptz) IS NULL OR a.occurred_at_utc < @ToUtc)
          AND (CAST(@Category AS text) IS NULL OR a.category = @Category)
          AND (CAST(@UserId AS uuid) IS NULL OR a.user_id = @UserId)
        """;

    public async Task<Result<PagedList<AuditLogListItem>>> Handle(
        GetAuditLogsQuery query,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<AuditLogListItem>(
            $"SELECT COUNT(*) FROM infrastructure.audit_logs a {Filter}",
            $"""
            SELECT {AuditLogSql.Columns}
            FROM infrastructure.audit_logs a
            {Filter}
            ORDER BY a.occurred_at_utc DESC, a.id DESC
            LIMIT @PageSize OFFSET @Offset
            """,
            query.Paging,
            new
            {
                query.FromUtc,
                query.ToUtc,
                Category = query.Category?.ToString(),
                query.UserId
            },
            cancellationToken);
    }
}

internal sealed class GetAuditLogByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetAuditLogByIdQuery, AuditLogListItem>
{
    public async Task<Result<AuditLogListItem>> Handle(GetAuditLogByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        AuditLogListItem? item = await connection.QuerySingleOrDefaultAsync<AuditLogListItem>(
            new CommandDefinition(
                $"SELECT {AuditLogSql.Columns} FROM infrastructure.audit_logs a WHERE a.id = @AuditLogId",
                new { query.AuditLogId },
                cancellationToken: cancellationToken));

        return item ?? Result.Failure<AuditLogListItem>(AuditLogErrors.NotFound(query.AuditLogId));
    }
}

internal static class AuditLogSql
{
    public const string Columns =
        """
        a.id AS Id, a.occurred_at_utc AS OccurredAtUtc, a.category AS Category, a.action AS Action,
        a.user_id AS UserId, a.user_email AS UserEmail, a.entity_type AS EntityType, a.entity_id AS EntityId,
        a.summary AS Summary, a.details::text AS Details, a.ip_address AS IpAddress, a.source AS Source
        """;
}
