namespace Domain.Production.DailyRecordings;

/// <summary>
/// Audit entry of a revision: why, who, when, and the values before the change (JSON).
/// </summary>
public sealed class DailyRecordingRevision
{
    internal DailyRecordingRevision(
        Guid dailyRecordingId,
        int revisionNumber,
        string reason,
        string previousValues,
        Guid? revisedBy,
        DateTime revisedAtUtc)
    {
        DailyRecordingId = dailyRecordingId;
        RevisionNumber = revisionNumber;
        Reason = reason;
        PreviousValues = previousValues;
        RevisedBy = revisedBy;
        RevisedAtUtc = revisedAtUtc;
    }

    private DailyRecordingRevision()
    {
    }

    public Guid DailyRecordingId { get; private set; }
    public int RevisionNumber { get; private set; }
    public string Reason { get; private set; }
    public string PreviousValues { get; private set; }
    public Guid? RevisedBy { get; private set; }
    public DateTime RevisedAtUtc { get; private set; }
}
