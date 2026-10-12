namespace Application.Approvals;

/// <summary>
/// A request in the inbox, "my submissions" and the WebApp list. Document type and status are the enum names.
/// </summary>
public sealed record ApprovalRequestResponse
{
    public Guid Id { get; init; }

    public string DocumentType { get; init; }

    public Guid DocumentId { get; init; }

    public string DocumentCode { get; init; }

    public string DocumentTitle { get; init; }

    public string? Summary { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public string BranchName { get; init; }

    public Guid RequestedBy { get; init; }

    public string RequestedByName { get; init; }

    public DateTime RequestedAtUtc { get; init; }

    public string Status { get; init; }

    /// <summary>
    /// The level waiting for a decision; once completed, the level that completed the request.
    /// </summary>
    public int CurrentLevel { get; init; }

    public int LevelCount { get; init; }

    public string? CurrentLevelName { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    /// <summary>
    /// Reason of the rejection, when rejected.
    /// </summary>
    public string? RejectReason { get; init; }

    /// <summary>
    /// Pending at a level without any approver able to decide (M-99): all inactive or without <c>approvals:decide</c>,
    /// and no active delegation to someone who can.
    /// </summary>
    public bool IsStuck { get; init; }
}

public sealed record ApprovalDetailResponse
{
    public ApprovalRequestResponse Request { get; init; }

    /// <summary>
    /// Old and new values, e.g. of a farmer's NIK and bank account (M-84).
    /// </summary>
    public IReadOnlyList<ApprovalChange> Changes { get; init; } = [];

    public IReadOnlyList<ApprovalLevelResponse> Levels { get; init; } = [];

    public IReadOnlyList<ApprovalDecisionResponse> Decisions { get; init; } = [];

    /// <summary>
    /// The signed-in user may approve or reject the current level.
    /// </summary>
    public bool CanDecide { get; init; }

    /// <summary>
    /// When <see cref="CanDecide"/> comes from a delegation: the approver decided for.
    /// </summary>
    public string? DecidesOnBehalfOf { get; init; }

    /// <summary>
    /// The signed-in user is the requester and the request is pending.
    /// </summary>
    public bool CanCancel { get; init; }
}

/// <param name="State">Approved, Rejected, Pending (current level), Waiting (later level) or Skipped.</param>
public sealed record ApprovalLevelResponse(int LevelNo, string Name, string State, IReadOnlyList<ApproverResponse> Approvers);

/// <param name="CanDecide">Active and holding <c>approvals:decide</c>.</param>
public sealed record ApproverResponse(Guid UserId, string Name, bool IsActive, bool CanDecide);

public sealed record ApprovalDecisionResponse(
    int LevelNo,
    string Outcome,
    Guid DecidedBy,
    string DecidedByName,
    Guid? OnBehalfOf,
    string? OnBehalfOfName,
    DateTime DecidedAtUtc,
    string? Note);

/// <summary>
/// Pending requests waiting for the signed-in user, per document type.
/// </summary>
public sealed record ApprovalInboxCountResponse(int Total, IReadOnlyDictionary<string, int> ByDocumentType);
