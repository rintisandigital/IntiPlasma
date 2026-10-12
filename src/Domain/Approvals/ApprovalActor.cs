namespace Domain.Approvals;

/// <summary>
/// The user deciding a level, optionally on behalf of the approver who delegated to them (M-98).
/// </summary>
public sealed record ApprovalActor(Guid UserId, Guid? OnBehalfOf = null)
{
    /// <summary>
    /// The listed approver whose place the actor takes.
    /// </summary>
    public Guid ApproverId => OnBehalfOf ?? UserId;

    public bool Involves(Guid userId) => userId == UserId || userId == OnBehalfOf;
}
