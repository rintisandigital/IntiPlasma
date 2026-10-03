using Application.Abstractions.Authorization;
using Domain.Access;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Infrastructure.Authorization;

/// <summary>
/// Computes a user's Web.App menu rights from their menu access profile (Full Access = every right each
/// usable menu offers). Cached per user with the <see cref="PermissionCacheKeys.Tag"/> tag, which profile and
/// menu changes invalidate across processes.
/// </summary>
internal sealed class MenuAccessProvider(ApplicationDbContext dbContext, HybridCache cache) : IMenuAccessProvider
{
    private static readonly HybridCacheEntryOptions CacheOptions = new() { Expiration = TimeSpan.FromMinutes(10) };
    private static readonly string[] CacheTags = [PermissionCacheKeys.Tag];

    public async Task<MenuAccess> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Dictionary<string, MenuRights> rights = await cache.GetOrCreateAsync(
            PermissionCacheKeys.MenuAccessForUser(userId),
            async token => await LoadAsync(userId, token),
            CacheOptions,
            CacheTags,
            cancellationToken);

        return new MenuAccess(rights);
    }

    private async Task<Dictionary<string, MenuRights>> LoadAsync(Guid userId, CancellationToken cancellationToken)
    {
        Guid? profileId = await dbContext.Users
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.MenuAccessProfileId)
            .SingleOrDefaultAsync(cancellationToken);

        if (profileId is null)
        {
            return [];
        }

        List<Menu> menus = await dbContext.Menus
            .AsNoTracking()
            .Where(m => m.ParentCode != null && m.InCatalog && m.IsAvailable && m.IsActive)
            .ToListAsync(cancellationToken);

        MenuAccessProfile? profile = await dbContext.MenuAccessProfiles
            .AsNoTracking()
            .Include(p => p.Items)
            .SingleOrDefaultAsync(p => p.Id == profileId, cancellationToken);

        if (profile is null)
        {
            return [];
        }

        if (profile.IsFullAccess)
        {
            return menus.ToDictionary(m => m.Code, m => m.SupportedRights, StringComparer.Ordinal);
        }

        var granted = profile.Items.ToDictionary(i => i.MenuId, i => i.Rights);

        return menus
            .Where(m => granted.ContainsKey(m.Id))
            .ToDictionary(m => m.Code, m => granted[m.Id] & m.SupportedRights, StringComparer.Ordinal);
    }
}
