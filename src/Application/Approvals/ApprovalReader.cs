using System.Data.Common;
using System.Text.Json;
using Dapper;
using Domain.Approvals;

namespace Application.Approvals;

/// <summary>
/// Reads requests with their levels, approvers and decisions (the timeline), from the point of view of one user.
/// </summary>
internal static class ApprovalReader
{
    public static async Task<IReadOnlyList<ApprovalDetailResponse>> LoadAsync(
        DbConnection connection,
        string where,
        object parameters,
        Guid userId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var dynamicParameters = new DynamicParameters(parameters);
        dynamicParameters.Add("UserId", userId);
        dynamicParameters.Add("Today", today);
        dynamicParameters.Add("DecidePermission", ApproverRules.DecidePermission);

        string sql =
            $"""
             {ApprovalSql.Select} WHERE {where} ORDER BY r.requested_at_utc DESC;

             SELECT r.id AS RequestId, r.payload AS Payload
             FROM approval.approval_requests r
             LEFT JOIN approval.approval_request_levels l ON l.request_id = r.id AND l.level_no = r.current_level
             WHERE {where};

             SELECT al.request_id AS RequestId, al.level_no AS LevelNo, al.name AS Name, al.approver_user_ids AS ApproverUserIds
             FROM approval.approval_request_levels al
             JOIN approval.approval_requests r ON r.id = al.request_id
             LEFT JOIN approval.approval_request_levels l ON l.request_id = r.id AND l.level_no = r.current_level
             WHERE {where};

             SELECT d.request_id AS RequestId, d.level_no AS LevelNo, d.outcome AS Outcome, d.decided_by AS DecidedBy,
                    d.on_behalf_of AS OnBehalfOf, d.decided_at_utc AS DecidedAtUtc, d.note AS Note
             FROM approval.approval_decisions d
             JOIN approval.approval_requests r ON r.id = d.request_id
             LEFT JOIN approval.approval_request_levels l ON l.request_id = r.id AND l.level_no = r.current_level
             WHERE {where}
             ORDER BY d.decided_at_utc;

             SELECT dg.delegator_user_id
             FROM approval.approval_delegations dg
             WHERE dg.delegate_user_id = @UserId AND dg.revoked_at_utc IS NULL AND @Today BETWEEN dg.from_date AND dg.to_date;

             SELECT EXISTS (SELECT 1 FROM identity.users me WHERE me.id = @UserId AND {ApproverRules.EligibleSql("me")});
             """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, dynamicParameters, cancellationToken: cancellationToken));

        List<ApprovalRequestResponse> requests = [.. await multi.ReadAsync<ApprovalRequestResponse>()];
        var payloads = (await multi.ReadAsync<PayloadRow>()).ToDictionary(p => p.RequestId, p => p.Payload);
        List<LevelRow> levels = [.. await multi.ReadAsync<LevelRow>()];
        List<DecisionRow> decisions = [.. await multi.ReadAsync<DecisionRow>()];
        HashSet<Guid> delegators = [.. await multi.ReadAsync<Guid>()];
        bool eligible = await multi.ReadSingleAsync<bool>();

        Dictionary<Guid, UserRow> users = await LoadUsersAsync(connection, levels, decisions, cancellationToken);

        return [.. requests.Select(r => Build(r, payloads, levels, decisions, users, userId, delegators, eligible))];
    }

    private static ApprovalDetailResponse Build(
        ApprovalRequestResponse request,
        Dictionary<Guid, string?> payloads,
        List<LevelRow> allLevels,
        List<DecisionRow> allDecisions,
        Dictionary<Guid, UserRow> users,
        Guid userId,
        HashSet<Guid> delegators,
        bool eligible)
    {
        List<DecisionRow> decisions = allDecisions.FindAll(d => d.RequestId == request.Id);
        List<LevelRow> levels = allLevels.FindAll(l => l.RequestId == request.Id);
        levels.Sort((a, b) => a.LevelNo.CompareTo(b.LevelNo));

        bool pending = request.Status == nameof(ApprovalStatus.Pending);
        LevelRow? current = pending ? levels.Find(l => l.LevelNo == request.CurrentLevel) : null;
        bool decidedBefore = decisions.Exists(d => d.DecidedBy == userId || d.OnBehalfOf == userId);

        Guid? onBehalfOf = null;
        bool canDecide = false;
        if (current is not null && eligible && request.RequestedBy != userId && !decidedBefore)
        {
            if (current.ApproverUserIds.Contains(userId))
            {
                canDecide = true;
            }
            else if (Array.Find(current.ApproverUserIds, id => delegators.Contains(id) && id != request.RequestedBy
                         && !decisions.Exists(d => d.DecidedBy == id || d.OnBehalfOf == id)) is var delegator
                     && delegator != Guid.Empty)
            {
                canDecide = true;
                onBehalfOf = delegator;
            }
        }

        return new ApprovalDetailResponse
        {
            Request = request,
            Changes = payloads.GetValueOrDefault(request.Id) is { } payload
                ? JsonSerializer.Deserialize<List<ApprovalChange>>(payload, ApprovalService.PayloadOptions) ?? []
                : [],
            Levels = [.. levels.Select(l => new ApprovalLevelResponse(
                l.LevelNo,
                l.Name,
                LevelState(request, l, decisions),
                [.. l.ApproverUserIds.Select(id => users.TryGetValue(id, out UserRow? u)
                    ? new ApproverResponse(id, u.Name, u.IsActive, u.CanDecide)
                    : new ApproverResponse(id, id.ToString(), false, false))]))],
            Decisions = [.. decisions.Select(d => new ApprovalDecisionResponse(
                d.LevelNo,
                d.Outcome,
                d.DecidedBy,
                NameOf(users, d.DecidedBy),
                d.OnBehalfOf,
                d.OnBehalfOf is { } other ? NameOf(users, other) : null,
                d.DecidedAtUtc,
                d.Note))],
            CanDecide = canDecide,
            DecidesOnBehalfOf = onBehalfOf is { } id2 ? NameOf(users, id2) : null,
            CanCancel = pending && request.RequestedBy == userId
        };
    }

    private static string LevelState(ApprovalRequestResponse request, LevelRow level, List<DecisionRow> decisions)
    {
        DecisionRow? decision = decisions.Find(d => d.LevelNo == level.LevelNo);
        if (decision is not null)
        {
            return decision.Outcome;
        }

        if (request.Status == nameof(ApprovalStatus.Pending))
        {
            return level.LevelNo == request.CurrentLevel ? "Pending" : "Waiting";
        }

        return "Skipped";
    }

    private static string NameOf(Dictionary<Guid, UserRow> users, Guid id) =>
        users.TryGetValue(id, out UserRow? user) ? user.Name : id.ToString();

    private static async Task<Dictionary<Guid, UserRow>> LoadUsersAsync(
        DbConnection connection,
        List<LevelRow> levels,
        List<DecisionRow> decisions,
        CancellationToken cancellationToken)
    {
        Guid[] ids = [.. levels.SelectMany(l => l.ApproverUserIds)
            .Concat(decisions.Select(d => d.DecidedBy))
            .Concat(decisions.Where(d => d.OnBehalfOf != null).Select(d => d.OnBehalfOf!.Value))
            .Distinct()];

        if (ids.Length == 0)
        {
            return [];
        }

        IEnumerable<UserRow> rows = await connection.QueryAsync<UserRow>(new CommandDefinition(
            $"""
             SELECT u.id AS Id, COALESCE(NULLIF(TRIM(CONCAT(u.first_name, ' ', u.last_name)), ''), u.email) AS Name,
                    u.is_active AS IsActive, ({ApproverRules.EligibleSql("u")}) AS CanDecide
             FROM identity.users u WHERE u.id = ANY(@Ids)
             """,
            new { Ids = ids, ApproverRules.DecidePermission },
            cancellationToken: cancellationToken));

        return rows.ToDictionary(u => u.Id);
    }

    internal sealed class PayloadRow
    {
        public Guid RequestId { get; set; }
        public string? Payload { get; set; }
    }

    internal sealed class LevelRow
    {
        public Guid RequestId { get; set; }
        public int LevelNo { get; set; }
        public string Name { get; set; }
        public Guid[] ApproverUserIds { get; set; } = [];
    }

    internal sealed class DecisionRow
    {
        public Guid RequestId { get; set; }
        public int LevelNo { get; set; }
        public string Outcome { get; set; }
        public Guid DecidedBy { get; set; }
        public Guid? OnBehalfOf { get; set; }
        public DateTime DecidedAtUtc { get; set; }
        public string? Note { get; set; }
    }

    internal sealed class UserRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public bool CanDecide { get; set; }
    }
}
