namespace Domain.Approvals;

/// <summary>
/// The decision of one level of an <see cref="ApprovalRequest"/>. A delegate decides on behalf of the approver who
/// delegated to them (M-98); the timeline then shows "delegate a.n. approver".
/// </summary>
public sealed class ApprovalDecision
{
    internal ApprovalDecision(
        Guid requestId,
        int levelNo,
        ApprovalOutcome outcome,
        ApprovalActor actor,
        string? note,
        DateTime decidedAtUtc)
    {
        Id = Guid.CreateVersion7();
        RequestId = requestId;
        LevelNo = levelNo;
        Outcome = outcome;
        DecidedBy = actor.UserId;
        OnBehalfOf = actor.OnBehalfOf;
        Note = note;
        DecidedAtUtc = decidedAtUtc;
    }

    private ApprovalDecision()
    {
    }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public int LevelNo { get; private set; }
    public ApprovalOutcome Outcome { get; private set; }
    public Guid DecidedBy { get; private set; }
    public Guid? OnBehalfOf { get; private set; }

    /// <summary>
    /// Optional note of an approval; the required reason of a rejection.
    /// </summary>
    public string? Note { get; private set; }

    public DateTime DecidedAtUtc { get; private set; }
}
