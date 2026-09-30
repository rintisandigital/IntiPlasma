namespace Domain.Roles;

/// <summary>
/// Catalog of every permission in the system. Permissions are granted to roles, roles are assigned to users.
/// Format: "{module}:{action}". New modules add their permissions here.
/// </summary>
public static class Permissions
{
    public const string UsersRead = "users:read";
    public const string UsersManage = "users:manage";
    public const string RolesRead = "roles:read";
    public const string RolesManage = "roles:manage";

    public static readonly IReadOnlyList<string> All =
    [
        UsersRead,
        UsersManage,
        RolesRead,
        RolesManage
    ];

    public static bool Exists(string permission) => All.Contains(permission);
}
