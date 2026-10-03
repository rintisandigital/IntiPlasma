using SharedKernel;

namespace Domain.Access;

public static class BranchAccessProfileErrors
{
    public static Error NotFound(Guid profileId) => Error.NotFound(
        "BranchAccessProfiles.NotFound",
        $"The branch access profile with the Id = '{profileId}' was not found");

    public static readonly Error NameNotUnique = Error.Conflict(
        "BranchAccessProfiles.NameNotUnique",
        "A branch access profile with this name already exists");

    public static readonly Error SystemReadOnly = Error.Problem(
        "BranchAccessProfiles.SystemReadOnly",
        "The All Branches profile is managed by the system and cannot be changed or deleted");

    public static Error InUse(int userCount) => Error.Conflict(
        "BranchAccessProfiles.InUse",
        $"The branch access profile is used by {userCount} user(s); assign them another profile first");

    public static readonly Error BranchRequired = Error.Problem(
        "BranchAccessProfiles.BranchRequired",
        "Select at least one branch, or choose All branches");
}
