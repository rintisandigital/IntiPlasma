using Domain.MasterData.Branches;
using SharedKernel;

namespace Application.Abstractions.Authorization;

public static class BranchAccessExtensions
{
    public static async Task<Result> EnsureAccessAsync(
        this IBranchAccess branchAccess,
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        return scope.CanAccess(branchId) ? Result.Success() : Result.Failure(BranchErrors.AccessDenied(branchId));
    }
}
