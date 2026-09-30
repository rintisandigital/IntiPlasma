namespace Domain.Users;

/// <summary>
/// A branch the user is allowed to work in (branch-scoped authorization).
/// </summary>
public sealed class UserBranch
{
    internal UserBranch(Guid userId, Guid branchId)
    {
        UserId = userId;
        BranchId = branchId;
    }

    public Guid UserId { get; private set; }
    public Guid BranchId { get; private set; }
}
