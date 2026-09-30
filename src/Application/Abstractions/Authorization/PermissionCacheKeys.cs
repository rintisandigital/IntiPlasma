namespace Application.Abstractions.Authorization;

public static class PermissionCacheKeys
{
    /// <summary>
    /// Every cached permission set and branch scope carries this tag, so a role change can invalidate them all at once.
    /// </summary>
    public const string Tag = "permissions";

    public static string ForUser(Guid userId) => $"permissions:user:{userId}";

    public static string BranchesForUser(Guid userId) => $"branches:user:{userId}";
}
