using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Branches;

public static class BranchErrors
{
    public static Error NotFound(Guid branchId) => Error.NotFound(
        "Branches.NotFound",
        $"The branch with the Id = '{branchId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("Branch", code);

    public static Error Inactive(Guid branchId) => CommonErrors.Inactive("Branch", branchId);

    public static Error AccessDenied(Guid branchId) => Error.Forbidden(
        "Branches.AccessDenied",
        $"You do not have access to the branch with the Id = '{branchId}'");
}
