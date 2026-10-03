using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Infrastructure.Authorization;

/// <summary>
/// Branch scope of the current user from their branch access profile ("Akses Cabang", PhaseW1): every branch
/// for an all-branches profile, otherwise the profile's active branches; no profile means no branch.
/// </summary>
internal sealed class BranchAccess(
    IUserContext userContext,
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

        CachedScope scope = await cache.GetOrCreateAsync(
            PermissionCacheKeys.BranchesForUser(userId),
            async token => await LoadAsync(userId, token),
            CacheOptions,
            CacheTags,
            cancellationToken);

        return scope.AllBranches ? BranchScope.All : new BranchScope(false, scope.BranchIds);
    }

    private async Task<CachedScope> LoadAsync(Guid userId, CancellationToken cancellationToken)
    {
        Guid? profileId = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.BranchAccessProfileId)
            .SingleOrDefaultAsync(cancellationToken);

        if (profileId is null)
        {
            return new CachedScope();
        }

        bool allBranches = await dbContext.BranchAccessProfiles
            .Where(p => p.Id == profileId)
            .Select(p => p.AllBranches)
            .SingleOrDefaultAsync(cancellationToken);

        if (allBranches)
        {
            return new CachedScope { AllBranches = true };
        }

        Guid[] branchIds = await dbContext.BranchAccessProfiles
            .Where(p => p.Id == profileId)
            .SelectMany(p => p.Branches)
            .Join(dbContext.Branches.Where(b => b.IsActive), pb => pb.BranchId, b => b.Id, (_, b) => b.Id)
            .ToArrayAsync(cancellationToken);

        return new CachedScope { BranchIds = branchIds };
    }

    internal sealed class CachedScope
    {
        public bool AllBranches { get; set; }

        public Guid[] BranchIds { get; set; } = [];
    }
}
