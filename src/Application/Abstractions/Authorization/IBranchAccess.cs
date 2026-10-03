namespace Application.Abstractions.Authorization;

/// <summary>
/// Branch-scoped authorization: which branches the current user may read and write.
/// </summary>
public interface IBranchAccess
{
    Task<BranchScope> GetScopeAsync(CancellationToken cancellationToken = default);
}

/// <param name="AllBranches">True when the user's branch access profile covers every branch.</param>
/// <param name="BranchIds">Active branches of the user's profile; ignored when <paramref name="AllBranches"/> is true.</param>
public sealed record BranchScope(bool AllBranches, Guid[] BranchIds)
{
    public static readonly BranchScope All = new(true, []);

    public bool CanAccess(Guid branchId) => AllBranches || BranchIds.Contains(branchId);
}
