using SharedKernel;

namespace Domain.Access;

public static class MenuAccessProfileErrors
{
    public static Error NotFound(Guid profileId) => Error.NotFound(
        "MenuAccessProfiles.NotFound",
        $"The menu access profile with the Id = '{profileId}' was not found");

    public static readonly Error NameNotUnique = Error.Conflict(
        "MenuAccessProfiles.NameNotUnique",
        "A menu access profile with this name already exists");

    public static readonly Error SystemReadOnly = Error.Problem(
        "MenuAccessProfiles.SystemReadOnly",
        "The Full Access profile is managed by the system and cannot be changed or deleted");

    public static Error InUse(int userCount) => Error.Conflict(
        "MenuAccessProfiles.InUse",
        $"The menu access profile is used by {userCount} user(s); assign them another profile first");

    public static Error ViewRequired(string menuCode) => Error.Problem(
        "MenuAccessProfiles.ViewRequired",
        $"Menu '{menuCode}': any right requires the View right");

    public static Error RightNotSupported(string menuCode) => Error.Problem(
        "MenuAccessProfiles.RightNotSupported",
        $"Menu '{menuCode}' does not offer one of the selected rights");
}
