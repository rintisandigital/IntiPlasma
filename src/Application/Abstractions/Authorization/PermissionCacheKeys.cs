namespace Application.Abstractions.Authorization;

public static class PermissionCacheKeys
{
    /// <summary>
    /// Every cached permission set carries this tag, so a role or user-role change can invalidate them all at once.
    /// </summary>
    public const string Tag = "permissions";

    public static string ForUser(Guid userId) => $"permissions:user:{userId}";
}
