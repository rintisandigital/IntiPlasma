using System.Data.Common;
using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Dapper;
using Domain.Approvals;
using SharedKernel;

namespace Application.Approvals;

/// <summary>
/// Pending requests whose current level the signed-in user may decide (PLAN-MOBILE §4.1), oldest first.
/// Not limited by the PPL scope, only by branch access (§4.5).
/// </summary>
public sealed record GetApprovalInboxQuery(PageRequest Paging, ApprovalDocumentType? DocumentType = null)
    : IQuery<PagedList<ApprovalRequestResponse>>;

public sealed record GetApprovalInboxCountQuery : IQuery<ApprovalInboxCountResponse>;

/// <summary>
/// Requests submitted by the signed-in user, newest first.
/// </summary>
public sealed record GetMySubmissionsQuery(PageRequest Paging, ApprovalStatus? Status = null)
    : IQuery<PagedList<ApprovalRequestResponse>>;

/// <summary>
/// One request with its timeline. Visible to the requester and to approvers with access to the branch; with
/// <paramref name="AnyInBranch"/> (WebApp Approval Requests page) to anyone with access to the branch.
/// </summary>
public sealed record GetApprovalRequestQuery(Guid RequestId, bool AnyInBranch = false) : IQuery<ApprovalDetailResponse>;

/// <summary>
/// Every request of a document (newest first), for the timeline on the document's own page. The caller checks
/// access to the document.
/// </summary>
internal sealed record GetDocumentApprovalsQuery(Guid DocumentId, IReadOnlyList<ApprovalDocumentType> DocumentTypes)
    : IQuery<IReadOnlyList<ApprovalDetailResponse>>;

/// <summary>
/// All requests of the branches the user may access (WebApp Approval Requests), newest first.
/// </summary>
public sealed record GetApprovalRequestsQuery(
    PageRequest Paging,
    ApprovalStatus? Status = null,
    ApprovalDocumentType? DocumentType = null,
    Guid? BranchId = null,
    bool StuckOnly = false) : IQuery<PagedList<ApprovalRequestResponse>>;

internal sealed class GetApprovalInboxQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : IQueryHandler<GetApprovalInboxQuery, PagedList<ApprovalRequestResponse>>
{
    public async Task<Result<PagedList<ApprovalRequestResponse>>> Handle(
        GetApprovalInboxQuery query,
        CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        string filter =
            $"""
             WHERE {ApprovalSql.WaitingFor} AND {ApprovalSql.BranchFilter}
               AND (@DocumentType::text IS NULL OR r.document_type = @DocumentType)
               AND (@Search IS NULL OR r.document_code ILIKE @Search OR r.document_title ILIKE @Search)
             """;

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<ApprovalRequestResponse>(
            $"""
             SELECT COUNT(*) FROM approval.approval_requests r
             LEFT JOIN approval.approval_request_levels l ON l.request_id = r.id AND l.level_no = r.current_level
             {filter}
             """,
            $"{ApprovalSql.Select} {filter} ORDER BY r.requested_at_utc LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                userContext.UserId,
                Today = ApproverRules.Today(dateTimeProvider.UtcNow),
                ApproverRules.DecidePermission,
                DocumentType = query.DocumentType?.ToString()
            },
            cancellationToken);
    }
}

internal sealed class GetApprovalInboxCountQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : IQueryHandler<GetApprovalInboxCountQuery, ApprovalInboxCountResponse>
{
    public async Task<Result<ApprovalInboxCountResponse>> Handle(
        GetApprovalInboxCountQuery query,
        CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        IEnumerable<(string DocumentType, int Count)> rows = await connection.QueryAsync<(string, int)>(new CommandDefinition(
            $"""
             SELECT r.document_type, COUNT(*)::int
             FROM approval.approval_requests r
             LEFT JOIN approval.approval_request_levels l ON l.request_id = r.id AND l.level_no = r.current_level
             WHERE {ApprovalSql.WaitingFor} AND {ApprovalSql.BranchFilter}
             GROUP BY r.document_type
             """,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                userContext.UserId,
                Today = ApproverRules.Today(dateTimeProvider.UtcNow),
                ApproverRules.DecidePermission
            },
            cancellationToken: cancellationToken));

        var byType = rows.ToDictionary(r => r.DocumentType, r => r.Count);

        return new ApprovalInboxCountResponse(byType.Values.Sum(), byType);
    }
}

internal sealed class GetMySubmissionsQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : IQueryHandler<GetMySubmissionsQuery, PagedList<ApprovalRequestResponse>>
{
    public async Task<Result<PagedList<ApprovalRequestResponse>>> Handle(
        GetMySubmissionsQuery query,
        CancellationToken cancellationToken)
    {
        const string filter =
            """
            WHERE r.requested_by = @UserId
              AND (@Status::text IS NULL OR r.status = @Status)
              AND (@Search IS NULL OR r.document_code ILIKE @Search OR r.document_title ILIKE @Search)
            """;

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<ApprovalRequestResponse>(
            $"SELECT COUNT(*) FROM approval.approval_requests r {filter}",
            $"{ApprovalSql.Select} {filter} ORDER BY r.requested_at_utc DESC LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                userContext.UserId,
                Today = ApproverRules.Today(dateTimeProvider.UtcNow),
                ApproverRules.DecidePermission,
                Status = query.Status?.ToString()
            },
            cancellationToken);
    }
}

internal sealed class GetApprovalRequestQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : IQueryHandler<GetApprovalRequestQuery, ApprovalDetailResponse>
{
    public async Task<Result<ApprovalDetailResponse>> Handle(GetApprovalRequestQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        IReadOnlyList<ApprovalDetailResponse> details = await ApprovalReader.LoadAsync(
            connection,
            "r.id = @RequestId",
            new { query.RequestId },
            userContext.UserId,
            ApproverRules.Today(dateTimeProvider.UtcNow),
            cancellationToken);

        ApprovalDetailResponse? detail = details.Count == 1 ? details[0] : null;
        if (detail is null)
        {
            return Result.Failure<ApprovalDetailResponse>(ApprovalErrors.RequestNotFound(query.RequestId));
        }

        if (detail.Request.RequestedBy == userContext.UserId)
        {
            return detail;
        }

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);
        bool visible = scope.CanAccess(detail.Request.BranchId)
            && (query.AnyInBranch || await ApproverRules.IsEligibleAsync(context, userContext.UserId, cancellationToken));

        return visible ? detail : Result.Failure<ApprovalDetailResponse>(ApprovalErrors.RequestNotFound(query.RequestId));
    }
}

internal sealed class GetDocumentApprovalsQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : IQueryHandler<GetDocumentApprovalsQuery, IReadOnlyList<ApprovalDetailResponse>>
{
    public async Task<Result<IReadOnlyList<ApprovalDetailResponse>>> Handle(
        GetDocumentApprovalsQuery query,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        IReadOnlyList<ApprovalDetailResponse> details = await ApprovalReader.LoadAsync(
            connection,
            "r.document_id = @DocumentId AND r.document_type = ANY(@DocumentTypes)",
            new { query.DocumentId, DocumentTypes = query.DocumentTypes.Select(t => t.ToString()).ToArray() },
            userContext.UserId,
            ApproverRules.Today(dateTimeProvider.UtcNow),
            cancellationToken);

        return Result.Success(details);
    }
}

internal sealed class GetApprovalRequestsQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IBranchAccess branchAccess,
    IDateTimeProvider dateTimeProvider) : IQueryHandler<GetApprovalRequestsQuery, PagedList<ApprovalRequestResponse>>
{
    public async Task<Result<PagedList<ApprovalRequestResponse>>> Handle(
        GetApprovalRequestsQuery query,
        CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        string filter =
            $"""
             WHERE {ApprovalSql.BranchFilter}
               AND (@Status::text IS NULL OR r.status = @Status)
               AND (@DocumentType::text IS NULL OR r.document_type = @DocumentType)
               AND (@BranchId::uuid IS NULL OR r.branch_id = @BranchId)
               AND (NOT @StuckOnly OR (r.status = 'Pending' AND {ApprovalSql.Stuck}))
               AND (@Search IS NULL OR r.document_code ILIKE @Search OR r.document_title ILIKE @Search)
             """;

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<ApprovalRequestResponse>(
            $"""
             SELECT COUNT(*) FROM approval.approval_requests r
             LEFT JOIN approval.approval_request_levels l ON l.request_id = r.id AND l.level_no = r.current_level
             {filter}
             """,
            $"{ApprovalSql.Select} {filter} ORDER BY r.requested_at_utc DESC LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                Today = ApproverRules.Today(dateTimeProvider.UtcNow),
                ApproverRules.DecidePermission,
                Status = query.Status?.ToString(),
                DocumentType = query.DocumentType?.ToString(),
                query.BranchId,
                query.StuckOnly
            },
            cancellationToken);
    }
}
