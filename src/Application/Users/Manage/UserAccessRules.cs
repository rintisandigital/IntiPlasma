using Application.Abstractions.Authorization;
using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Domain.Access;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Manage;

/// <summary>
/// Shared checks of the user management use cases (PLAN-WEBAPP §11.4).
/// </summary>
internal static class UserAccessRules
{
    /// <summary>
    /// The profiles exist and the default branch is an active branch covered by the branch access profile.
    /// </summary>
    public static async Task<Result> ValidateAccessAsync(
        IApplicationDbContext context,
        Guid? menuAccessProfileId,
        Guid? branchAccessProfileId,
        Guid? defaultBranchId,
        CancellationToken cancellationToken)
    {
        if (menuAccessProfileId is Guid menuProfileId &&
            !await context.MenuAccessProfiles.AnyAsync(p => p.Id == menuProfileId, cancellationToken))
        {
            return Result.Failure(MenuAccessProfileErrors.NotFound(menuProfileId));
        }

        if (branchAccessProfileId is not Guid branchProfileId)
        {
            return defaultBranchId is null ? Result.Success() : Result.Failure(UserErrors.DefaultBranchNotAccessible);
        }

        BranchAccessProfile? branchProfile = await context.BranchAccessProfiles
            .AsNoTracking()
            .Include(p => p.Branches)
            .SingleOrDefaultAsync(p => p.Id == branchProfileId, cancellationToken);

        if (branchProfile is null)
        {
            return Result.Failure(BranchAccessProfileErrors.NotFound(branchProfileId));
        }

        if (defaultBranchId is Guid branchId &&
            (!branchProfile.Covers(branchId) ||
             !await context.Branches.AnyAsync(b => b.Id == branchId && b.IsActive, cancellationToken)))
        {
            return Result.Failure(UserErrors.DefaultBranchNotAccessible);
        }

        return Result.Success();
    }

    /// <summary>
    /// Fails when <paramref name="user"/> is the last active user with Full Access (the application would be
    /// left without anyone able to manage access).
    /// </summary>
    public static async Task<Result> EnsureNotLastAdministratorAsync(
        IApplicationDbContext context,
        User user,
        CancellationToken cancellationToken)
    {
        if (!user.IsActive || user.MenuAccessProfileId != MenuAccessProfile.FullAccessId)
        {
            return Result.Success();
        }

        bool anotherAdministrator = await context.Users.AnyAsync(
            u => u.Id != user.Id && u.IsActive && u.MenuAccessProfileId == MenuAccessProfile.FullAccessId,
            cancellationToken);

        return anotherAdministrator ? Result.Success() : Result.Failure(UserErrors.LastAdministrator);
    }

    /// <summary>
    /// Drops every cached access entry of the user in all processes (permissions, branches, menus, session).
    /// </summary>
    public static async Task InvalidateAsync(ICacheInvalidator cache, Guid userId, CancellationToken cancellationToken)
    {
        foreach (string key in PermissionCacheKeys.AllForUser(userId))
        {
            await cache.RemoveAsync(key, cancellationToken);
        }
    }
}
