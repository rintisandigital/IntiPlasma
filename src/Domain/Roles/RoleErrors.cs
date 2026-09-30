using SharedKernel;

namespace Domain.Roles;

public static class RoleErrors
{
    public static Error NotFound(Guid roleId) => Error.NotFound(
        "Roles.NotFound",
        $"The role with the Id = '{roleId}' was not found");

    public static Error UnknownPermission(string permission) => Error.Problem(
        "Roles.UnknownPermission",
        $"The permission '{permission}' does not exist");

    public static readonly Error NameNotUnique = Error.Conflict(
        "Roles.NameNotUnique",
        "The provided role name is not unique");

    public static readonly Error SystemRoleIsReadOnly = Error.Problem(
        "Roles.SystemRoleIsReadOnly",
        "System roles cannot be modified");
}
