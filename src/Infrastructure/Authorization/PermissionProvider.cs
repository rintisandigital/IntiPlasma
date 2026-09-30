using Application.Abstractions.Authorization;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Infrastructure.Authorization;

internal sealed class PermissionProvider(ApplicationDbContext dbContext, HybridCache cache)
{
    private static readonly HybridCacheEntryOptions CacheOptions = new() { Expiration = TimeSpan.FromMinutes(10) };
    private static readonly string[] CacheTags = [PermissionCacheKeys.Tag];

    public async Task<HashSet<string>> GetForUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        string[] permissions = await cache.GetOrCreateAsync(
            PermissionCacheKeys.ForUser(userId),
            async token => await dbContext.Users
                .Where(u => u.Id == userId)
                .SelectMany(u => u.Roles)
                .Join(dbContext.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r)
                .SelectMany(r => r.Permissions)
                .Select(rp => rp.Permission)
                .Distinct()
                .ToArrayAsync(token),
            CacheOptions,
            CacheTags,
            cancellationToken);

        return [.. permissions];
    }
}
