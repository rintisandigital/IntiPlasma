namespace Application.Abstractions.Authorization;

/// <summary>
/// Branch-scoped authorization: which branches the current user may read and write.
/// </summary>
public interface IBranchAccess
{
    Task<BranchScope> GetScopeAsync(CancellationToken cancellationToken = default);
}

/// <param name="AllBranches">True for head office users (permission <c>branches:access-all</c>).</param>
/// <param name="BranchIds">Branches assigned to the user; ignored when <paramref name="AllBranches"/> is true.</param>
public sealed record BranchScope(bool AllBranches, Guid[] BranchIds)
{
    public static readonly BranchScope All = new(true, []);

    public bool CanAccess(Guid branchId) => AllBranches || BranchIds.Contains(branchId);
}
