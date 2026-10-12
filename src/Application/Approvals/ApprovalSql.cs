namespace Application.Approvals;

/// <summary>
/// SQL shared by the approval queries. Parameters: <c>@UserId</c>, <c>@Today</c> (Jakarta date) and
/// <c>@DecidePermission</c>.
/// </summary>
internal static class ApprovalSql
{
    /// <summary>
    /// The current level (alias <c>l</c>) has nobody who can decide it (M-99).
    /// </summary>
    public static readonly string Stuck =
        $"""
         NOT EXISTS (SELECT 1 FROM identity.users su
                     WHERE su.id = ANY(l.approver_user_ids) AND su.id <> r.requested_by
                       AND {ApproverRules.EligibleSql("su")})
         AND NOT EXISTS (SELECT 1 FROM approval.approval_delegations sd
                         JOIN identity.users sdu ON sdu.id = sd.delegate_user_id
                         WHERE sd.delegator_user_id = ANY(l.approver_user_ids) AND sd.revoked_at_utc IS NULL
                           AND @Today BETWEEN sd.from_date AND sd.to_date AND sdu.id <> r.requested_by
                           AND {ApproverRules.EligibleSql("sdu")})
         """;

    /// <summary>
    /// Pending requests whose current level the user may decide, as an approver or a delegate (M-98), and that they
    /// have not decided at another level. Branch access is filtered by the caller.
    /// </summary>
    public static readonly string WaitingFor =
        $"""
         r.status = 'Pending'
         AND r.requested_by <> @UserId
         AND EXISTS (SELECT 1 FROM identity.users me WHERE me.id = @UserId AND {ApproverRules.EligibleSql("me")})
         AND (@UserId = ANY(l.approver_user_ids)
              OR EXISTS (SELECT 1 FROM approval.approval_delegations dg
                         WHERE dg.delegate_user_id = @UserId AND dg.revoked_at_utc IS NULL
                           AND @Today BETWEEN dg.from_date AND dg.to_date
                           AND dg.delegator_user_id = ANY(l.approver_user_ids)
                           AND dg.delegator_user_id <> r.requested_by))
         AND NOT EXISTS (SELECT 1 FROM approval.approval_decisions dd
                         WHERE dd.request_id = r.id AND (dd.decided_by = @UserId OR dd.on_behalf_of = @UserId))
         """;

    public static readonly string Select =
        $"""
         SELECT r.id AS Id, r.document_type AS DocumentType, r.document_id AS DocumentId, r.document_code AS DocumentCode,
                r.document_title AS DocumentTitle, r.summary AS Summary, r.branch_id AS BranchId, b.code AS BranchCode,
                b.name AS BranchName, r.requested_by AS RequestedBy,
                COALESCE(NULLIF(TRIM(CONCAT(u.first_name, ' ', u.last_name)), ''), u.email) AS RequestedByName,
                r.requested_at_utc AS RequestedAtUtc, r.status AS Status, r.current_level AS CurrentLevel,
                r.level_count AS LevelCount, l.name AS CurrentLevelName, r.completed_at_utc AS CompletedAtUtc,
                (SELECT d.note FROM approval.approval_decisions d
                 WHERE d.request_id = r.id AND d.outcome = 'Rejected' LIMIT 1) AS RejectReason,
                (r.status = 'Pending' AND {Stuck}) AS IsStuck
         FROM approval.approval_requests r
         JOIN master.branches b ON b.id = r.branch_id
         JOIN identity.users u ON u.id = r.requested_by
         LEFT JOIN approval.approval_request_levels l ON l.request_id = r.id AND l.level_no = r.current_level
         """;

    /// <summary>
    /// Columns of the latest request of a document (M-89), selected from <see cref="LatestJoin"/> (alias <c>la</c>).
    /// </summary>
    public const string LatestColumns =
        """
        la.id AS ApprovalRequestId, la.document_type AS ApprovalDocumentType, la.status AS ApprovalStatus,
        la.current_level AS ApprovalLevel, la.level_count AS ApprovalLevelCount, la.level_name AS ApprovalLevelName,
        la.reject_reason AS ApprovalRejectReason
        """;

    /// <summary>
    /// Joins the latest request of the document <paramref name="alias"/> of the given types as <c>la</c>.
    /// </summary>
    public static string LatestJoin(string alias, params string[] documentTypes) =>
        $"""
         LEFT JOIN LATERAL (
             SELECT ar.id, ar.document_type, ar.status, ar.current_level, ar.level_count, arl.name AS level_name,
                    (SELECT ad.note FROM approval.approval_decisions ad
                     WHERE ad.request_id = ar.id AND ad.outcome = 'Rejected' LIMIT 1) AS reject_reason
             FROM approval.approval_requests ar
             LEFT JOIN approval.approval_request_levels arl ON arl.request_id = ar.id AND arl.level_no = ar.current_level
             WHERE ar.document_id = {alias}.id
               AND ar.document_type IN ({string.Join(", ", documentTypes.Select(t => $"'{t}'"))})
             ORDER BY ar.requested_at_utc DESC
             LIMIT 1) la ON TRUE
         """;

    public const string BranchFilter = "(@AllBranches OR r.branch_id = ANY(@BranchIds))";
}
