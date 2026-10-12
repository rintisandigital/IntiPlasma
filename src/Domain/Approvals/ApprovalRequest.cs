using SharedKernel;

namespace Domain.Approvals;

/// <summary>
/// Pengajuan approval of one document (PLAN-MOBILE §5.1-5.2). The levels of the flow are copied when the request is
/// submitted (M-32) and decided one after another: every level approving makes the request Approved, any level
/// rejecting makes it Rejected (M-12). The requester can cancel while it is pending (M-33). The effect on the document
/// is applied by the caller in the same save as the last decision.
/// </summary>
public sealed class ApprovalRequest : AggregateRoot
{
    private readonly List<ApprovalRequestLevel> _levels = [];
    private readonly List<ApprovalDecision> _decisions = [];

    private ApprovalRequest(Guid id, ApprovalDocumentType documentType, Guid documentId, Guid branchId)
        : base(id)
    {
        DocumentType = documentType;
        DocumentId = documentId;
        BranchId = branchId;
        Status = ApprovalStatus.Pending;
        CurrentLevel = 1;
    }

    private ApprovalRequest()
    {
    }

    public ApprovalDocumentType DocumentType { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid BranchId { get; private set; }

    /// <summary>
    /// Code of the document (farmer, coop or contract code) for lists and the inbox.
    /// </summary>
    public string DocumentCode { get; private set; }

    public string DocumentTitle { get; private set; }

    /// <summary>
    /// Short description for the inbox, e.g. "Plasma · NIK 3201…".
    /// </summary>
    public string? Summary { get; private set; }

    /// <summary>
    /// JSON shown to the approver, e.g. the old and new values of a farmer change.
    /// </summary>
    public string? Payload { get; private set; }

    public Guid? FlowId { get; private set; }
    public Guid RequestedBy { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public ApprovalStatus Status { get; private set; }

    /// <summary>
    /// The level waiting for a decision; after the request is completed, the level that completed it.
    /// </summary>
    public int CurrentLevel { get; private set; }

    public int LevelCount { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public IReadOnlyList<ApprovalRequestLevel> Levels => [.. _levels.OrderBy(l => l.LevelNo)];
    public IReadOnlyList<ApprovalDecision> Decisions => [.. _decisions.OrderBy(d => d.DecidedAtUtc)];

    public ApprovalRequestLevel? PendingLevel =>
        Status == ApprovalStatus.Pending ? _levels.Find(l => l.LevelNo == CurrentLevel) : null;

    public static ApprovalRequest Submit(
        ApprovalDocumentType documentType,
        Guid documentId,
        Guid branchId,
        string documentCode,
        string documentTitle,
        string? summary,
        string? payload,
        Guid? flowId,
        IReadOnlyList<ApprovalLevelDefinition> levels,
        Guid requestedBy,
        DateTime utcNow)
    {
        var request = new ApprovalRequest(Guid.CreateVersion7(), documentType, documentId, branchId)
        {
            DocumentCode = documentCode,
            DocumentTitle = documentTitle,
            Summary = summary,
            Payload = payload,
            FlowId = flowId,
            RequestedBy = requestedBy,
            RequestedAtUtc = utcNow,
            LevelCount = levels.Count
        };

        for (int i = 0; i < levels.Count; i++)
        {
            request._levels.Add(new ApprovalRequestLevel(request.Id, i + 1, levels[i]));
        }

        request.Raise(new ApprovalRequestSubmittedDomainEvent(request.Id));

        return request;
    }

    /// <summary>
    /// Whether <paramref name="userId"/> may decide the current level, either as a listed approver or on behalf of one.
    /// </summary>
    public bool IsApproverOfCurrentLevel(Guid userId) => PendingLevel?.ApproverUserIds.Contains(userId) == true;

    public Result Approve(ApprovalActor actor, string? note, DateTime utcNow)
    {
        Result allowed = EnsureCanDecide(actor);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        _decisions.Add(new ApprovalDecision(Id, CurrentLevel, ApprovalOutcome.Approved, actor, Clean(note), utcNow));

        if (CurrentLevel < LevelCount)
        {
            CurrentLevel++;
            Raise(new ApprovalLevelApprovedDomainEvent(Id, CurrentLevel - 1));

            return Result.Success();
        }

        Status = ApprovalStatus.Approved;
        CompletedAtUtc = utcNow;
        Raise(new ApprovalRequestApprovedDomainEvent(Id));

        return Result.Success();
    }

    public Result Reject(ApprovalActor actor, string? reason, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(ApprovalErrors.ReasonRequired);
        }

        Result allowed = EnsureCanDecide(actor);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        _decisions.Add(new ApprovalDecision(Id, CurrentLevel, ApprovalOutcome.Rejected, actor, reason.Trim(), utcNow));

        Status = ApprovalStatus.Rejected;
        CompletedAtUtc = utcNow;
        Raise(new ApprovalRequestRejectedDomainEvent(Id));

        return Result.Success();
    }

    public Result Cancel(Guid userId, DateTime utcNow)
    {
        if (Status != ApprovalStatus.Pending)
        {
            return Result.Failure(ApprovalErrors.NotPending);
        }

        if (userId != RequestedBy)
        {
            return Result.Failure(ApprovalErrors.OnlyRequesterCanCancel);
        }

        Status = ApprovalStatus.Cancelled;
        CompletedAtUtc = utcNow;
        Raise(new ApprovalRequestCancelledDomainEvent(Id));

        return Result.Success();
    }

    /// <summary>
    /// Replaces an approver of a level that has not been decided yet (M-32), e.g. one who is on leave or inactive.
    /// </summary>
    public Result ReassignApprover(int levelNo, Guid fromUserId, Guid toUserId)
    {
        if (Status != ApprovalStatus.Pending)
        {
            return Result.Failure(ApprovalErrors.NotPending);
        }

        if (levelNo < CurrentLevel)
        {
            return Result.Failure(ApprovalErrors.LevelAlreadyDecided);
        }

        ApprovalRequestLevel? level = _levels.Find(l => l.LevelNo == levelNo);
        if (level is null || !level.Replace(fromUserId, toUserId))
        {
            return Result.Failure(ApprovalErrors.ApproverNotInLevel);
        }

        return Result.Success();
    }

    /// <summary>
    /// Replaces an approver in every level still to be decided (M-99). Returns whether anything changed.
    /// </summary>
    public bool ReplaceApprover(Guid fromUserId, Guid toUserId)
    {
        if (Status != ApprovalStatus.Pending)
        {
            return false;
        }

        bool changed = false;

        foreach (ApprovalRequestLevel level in _levels.Where(l => l.LevelNo >= CurrentLevel))
        {
            changed |= level.Replace(fromUserId, toUserId);
        }

        return changed;
    }

    private Result EnsureCanDecide(ApprovalActor actor)
    {
        if (Status != ApprovalStatus.Pending)
        {
            return Result.Failure(ApprovalErrors.NotPending);
        }

        if (actor.UserId == RequestedBy || actor.OnBehalfOf == RequestedBy)
        {
            return Result.Failure(ApprovalErrors.RequesterCannotDecide);
        }

        if (!IsApproverOfCurrentLevel(actor.ApproverId))
        {
            return Result.Failure(ApprovalErrors.NotApprover);
        }

        bool decidedBefore = _decisions.Exists(d => actor.Involves(d.DecidedBy) || d.OnBehalfOf is { } other && actor.Involves(other));

        return decidedBefore ? Result.Failure(ApprovalErrors.AlreadyDecidedOtherLevel) : Result.Success();
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
