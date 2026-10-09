namespace Application.Abstractions.Authorization;

public static class PermissionCacheKeys
{
    /// <summary>
    /// Every cached permission set and branch scope carries this tag, so a role change can invalidate them all at once.
    /// </summary>
    public const string Tag = "permissions";

    public static string ForUser(Guid userId) => $"permissions:user:{userId}";

    public static string BranchesForUser(Guid userId) => $"branches:user:{userId}";

    /// <summary>
    /// Whether the user is limited to the farmers and coops assigned to them (PPL scope).
    /// </summary>
    public static string FieldScopeForUser(Guid userId) => $"field-scope:user:{userId}";

    /// <summary>
    /// Web.App menu rights of the user (from the menu access profile).
    /// </summary>
    public static string MenuAccessForUser(Guid userId) => $"menu-access:user:{userId}";

    /// <summary>
    /// Every per-user access entry, for invalidation after a change to that user.
    /// </summary>
    public static IEnumerable<string> AllForUser(Guid userId) =>
    [
        ForUser(userId),
        BranchesForUser(userId),
        FieldScopeForUser(userId),
        MenuAccessForUser(userId),
        SessionForUser(userId)
    ];

    /// <summary>
    /// Cookie session data (active flag + security stamp) re-validated by Web.App.
    /// </summary>
    public static string SessionForUser(Guid userId) => $"session:user:{userId}";
}
