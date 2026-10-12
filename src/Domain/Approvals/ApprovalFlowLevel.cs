namespace Domain.Approvals;

/// <summary>
/// One level of an <see cref="ApprovalFlow"/>.
/// </summary>
public sealed class ApprovalFlowLevel
{
    internal ApprovalFlowLevel(Guid flowId, int levelNo, ApprovalLevelDefinition definition)
    {
        FlowId = flowId;
        LevelNo = levelNo;
        Set(definition);
    }

    private ApprovalFlowLevel()
    {
    }

    public Guid FlowId { get; private set; }
    public int LevelNo { get; private set; }
    public string Name { get; private set; }
    public Guid[] ApproverUserIds { get; private set; } = [];

    internal void Set(ApprovalLevelDefinition definition)
    {
        Name = definition.Name.Trim();
        ApproverUserIds = [.. definition.ApproverUserIds.Where(id => id != Guid.Empty).Distinct()];
    }

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
