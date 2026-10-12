namespace MobileApp.Core.Contracts;

/// <summary>
/// The latest approval request of a farmer, coop or contract (PLAN-MOBILE M-89), as flat fields of their responses.
/// All null when the record never went through the approval module (data from before M6).
/// </summary>
public abstract record ApprovalTracked
{
    public Guid? ApprovalRequestId { get; init; }

    /// <summary>
    /// <c>Farmer</c>, <c>FarmerChange</c>, <c>Coop</c> or <c>Contract</c>.
    /// </summary>
    public string? ApprovalDocumentType { get; init; }

    /// <summary>
    /// <c>Pending</c>, <c>Approved</c>, <c>Rejected</c> or <c>Cancelled</c>.
    /// </summary>
    public string? ApprovalStatus { get; init; }

    public int? ApprovalLevel { get; init; }

    public int? ApprovalLevelCount { get; init; }

    public string? ApprovalLevelName { get; init; }

    public string? ApprovalRejectReason { get; init; }

    public bool IsAwaitingApproval => ApprovalStatus == "Pending";
}

/// <summary>
/// A request in <c>approvals/inbox</c> and <c>approvals/mine</c>.
/// </summary>
public sealed record ApprovalRequest
{
    public Guid Id { get; init; }

    public string DocumentType { get; init; } = string.Empty;

    public Guid DocumentId { get; init; }

    public string DocumentCode { get; init; } = string.Empty;

    public string DocumentTitle { get; init; } = string.Empty;

    public string? Summary { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; } = string.Empty;

    public string BranchName { get; init; } = string.Empty;

    public Guid RequestedBy { get; init; }

    public string RequestedByName { get; init; } = string.Empty;

    public DateTime RequestedAtUtc { get; init; }

    public string Status { get; init; } = string.Empty;

    public int CurrentLevel { get; init; }

    public int LevelCount { get; init; }

    public string? CurrentLevelName { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public string? RejectReason { get; init; }

    public bool IsStuck { get; init; }
}

/// <summary>
/// <c>approvals/{id}</c> and the <c>…/approvals</c> timeline of a document.
/// </summary>
public sealed record ApprovalDetail
{
    public ApprovalRequest Request { get; init; } = new();

    public IReadOnlyList<ApprovalChange> Changes { get; init; } = [];

    public IReadOnlyList<ApprovalLevel> Levels { get; init; } = [];

    public IReadOnlyList<ApprovalDecision> Decisions { get; init; } = [];

    public bool CanDecide { get; init; }

    /// <summary>
    /// When the user decides as a delegate: the approver they decide for (M-98).
    /// </summary>
    public string? DecidesOnBehalfOf { get; init; }

    public bool CanCancel { get; init; }
}

public sealed record ApprovalChange(string Field, string? OldValue, string? NewValue);

/// <param name="State"><c>Approved</c>, <c>Rejected</c>, <c>Pending</c>, <c>Waiting</c> or <c>Skipped</c>.</param>
public sealed record ApprovalLevel(int LevelNo, string Name, string State, IReadOnlyList<Approver> Approvers);

public sealed record Approver(Guid UserId, string Name, bool IsActive, bool CanDecide);

public sealed record ApprovalDecision(
    int LevelNo,
    string Outcome,
    Guid DecidedBy,
    string DecidedByName,
    Guid? OnBehalfOf,
    string? OnBehalfOfName,
    DateTime DecidedAtUtc,
    string? Note);

/// <summary>
/// <c>approvals/inbox/count</c>.
/// </summary>
public sealed record ApprovalInboxCount
{
    public int Total { get; init; }

    public IReadOnlyDictionary<string, int> ByDocumentType { get; init; } = new Dictionary<string, int>();
}

/// <param name="Status"><c>Active</c>, <c>Scheduled</c>, <c>Ended</c> or <c>Revoked</c>.</param>
public sealed record ApprovalDelegation(
    Guid Id,
    Guid DelegatorUserId,
    string DelegatorName,
    Guid DelegateUserId,
    string DelegateName,
    DateOnly FromDate,
    DateOnly ToDate,
    string? Reason,
    string Status,
    DateTime CreatedAtUtc);

/// <summary>
/// <c>approvals/delegates</c>: approvers a delegation can go to.
/// </summary>
public sealed record ApproverCandidate(Guid Id, string Name, string Email, bool CoversBranch);

/// <summary>
/// Approval part of <c>mobile/dashboard</c> (M-93).
/// </summary>
public sealed record DashboardApprovals
{
    public int WaitingForMe { get; init; }

    public IReadOnlyDictionary<string, int> WaitingByDocumentType { get; init; } = new Dictionary<string, int>();

    public int MyPending { get; init; }

    public IReadOnlyList<DashboardRejectedRequest> MyRejected { get; init; } = [];
}

public sealed record DashboardRejectedRequest(
    Guid RequestId,
    string DocumentType,
    Guid DocumentId,
    string DocumentCode,
    string DocumentTitle,
    string? RejectReason,
    DateTime RejectedAtUtc);

public sealed record FarmerPendingChange
{
    public string? Nik { get; init; }

    public BankAccount BankAccount { get; init; } = new();
}
