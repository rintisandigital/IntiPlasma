using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Domain.Roles;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Infrastructure.Authorization;

internal sealed class BranchAccess(
    IUserContext userContext,
    PermissionProvider permissionProvider,
    ApplicationDbContext dbContext,
    HybridCache cache) : IBranchAccess
{
    private static readonly BranchScope None = new(false, []);
    private static readonly HybridCacheEntryOptions CacheOptions = new() { Expiration = TimeSpan.FromMinutes(10) };
    private static readonly string[] CacheTags = [PermissionCacheKeys.Tag];

    public async Task<BranchScope> GetScopeAsync(CancellationToken cancellationToken = default)
    {
        if (!userContext.IsAuthenticated)
        {
            return None;
        }

        Guid userId = userContext.UserId;

        HashSet<string> permissions = await permissionProvider.GetForUserIdAsync(userId, cancellationToken);
        if (permissions.Contains(Permissions.BranchesAccessAll))
        {
            return BranchScope.All;
        }

        Guid[] branchIds = await cache.GetOrCreateAsync(
            PermissionCacheKeys.BranchesForUser(userId),
            async token => await dbContext.Users
                .Where(u => u.Id == userId)
                .SelectMany(u => u.Branches)
                .Select(b => b.BranchId)
                .ToArrayAsync(token),
            CacheOptions,
            CacheTags,
            cancellationToken);

        return new BranchScope(false, branchIds);
    }
}
