namespace Domain.Approvals;

/// <summary>
/// A level of an <see cref="ApprovalRequest"/>, copied from the flow when the request was submitted.
/// </summary>
public sealed class ApprovalRequestLevel
{
    internal ApprovalRequestLevel(Guid requestId, int levelNo, ApprovalLevelDefinition definition)
    {
        RequestId = requestId;
        LevelNo = levelNo;
        Name = definition.Name.Trim();
        ApproverUserIds = [.. definition.ApproverUserIds.Where(id => id != Guid.Empty).Distinct()];
    }

    private ApprovalRequestLevel()
    {
    }

    public Guid RequestId { get; private set; }
    public int LevelNo { get; private set; }
    public string Name { get; private set; }
    public Guid[] ApproverUserIds { get; private set; } = [];

    internal bool Replace(Guid fromUserId, Guid toUserId)
    {
        if (!ApproverUserIds.Contains(fromUserId))
        {
            return false;
        }

        ApproverUserIds = [.. ApproverUserIds.Select(id => id == fromUserId ? toUserId : id).Distinct()];

        return true;
    }
}
