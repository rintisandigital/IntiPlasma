using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Domain.Roles;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Infrastructure.Authorization;

/// <summary>
/// PPL scope of the current user (Web.Api): restricted when a role grants <c>partnership:assigned-only</c> and the
/// user is not in the system Administrator role (which holds every permission).
/// </summary>
internal sealed class FieldScopeProvider(
    IUserContext userContext,
    ApplicationDbContext dbContext,
    PermissionProvider permissionProvider,
    HybridCache cache) : IFieldScope
{
    private static readonly HybridCacheEntryOptions CacheOptions = new() { Expiration = TimeSpan.FromMinutes(10) };
    private static readonly string[] CacheTags = [PermissionCacheKeys.Tag];

    private FieldScope? _scope;

    public async Task<FieldScope> GetScopeAsync(CancellationToken cancellationToken = default)
    {
        if (_scope is not null)
        {
            return _scope;
        }

        if (!userContext.IsAuthenticated)
        {
            return FieldScope.Unrestricted;
        }

        Guid userId = userContext.UserId;

        bool restricted = await cache.GetOrCreateAsync(
            PermissionCacheKeys.FieldScopeForUser(userId),
            async token => await IsRestrictedAsync(userId, token),
            CacheOptions,
            CacheTags,
            cancellationToken);

        _scope = restricted ? new FieldScope(true, userId) : FieldScope.Unrestricted;

        return _scope;
    }

    public async Task<bool> CanAccessFarmerAsync(Guid farmerId, CancellationToken cancellationToken = default)
    {
        FieldScope scope = await GetScopeAsync(cancellationToken);

        return !scope.Restricted ||
               await dbContext.Farmers.AnyAsync(
                   f => f.Id == farmerId &&
                        (f.FieldOfficerUserId == scope.UserId ||
                         dbContext.Coops.Any(c => c.FarmerId == f.Id && c.FieldOfficerUserId == scope.UserId)),
                   cancellationToken);
    }

    public async Task<bool> CanAccessCoopAsync(Guid coopId, CancellationToken cancellationToken = default)
    {
        FieldScope scope = await GetScopeAsync(cancellationToken);

        return !scope.Restricted ||
               await dbContext.Coops.AnyAsync(c => c.Id == coopId && c.FieldOfficerUserId == scope.UserId, cancellationToken);
    }

    public async Task<bool> CanAccessCycleAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        FieldScope scope = await GetScopeAsync(cancellationToken);

        return !scope.Restricted ||
               await dbContext.ProductionCycles
                   .Where(pc => pc.Id == cycleId)
                   .Join(dbContext.Coops, pc => pc.CoopId, c => c.Id, (_, c) => c)
                   .AnyAsync(c => c.FieldOfficerUserId == scope.UserId, cancellationToken);
    }

    private async Task<bool> IsRestrictedAsync(Guid userId, CancellationToken cancellationToken)
    {
        HashSet<string> permissions = await permissionProvider.GetForUserIdAsync(userId, cancellationToken);

        if (!permissions.Contains(Permissions.PartnershipAssignedOnly))
        {
            return false;
        }

        bool administrator = await dbContext.Users
            .Where(u => u.Id == userId)
            .SelectMany(u => u.Roles)
            .Join(dbContext.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r)
            .AnyAsync(r => r.IsSystem && r.Name == Role.AdministratorName, cancellationToken);

        return !administrator;
    }
}
