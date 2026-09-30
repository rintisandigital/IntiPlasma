using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Roles;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Users.AssignRoles;

internal sealed class AssignUserRolesCommandHandler(IApplicationDbContext context, HybridCache cache)
    : ICommandHandler<AssignUserRolesCommand>
{
    public async Task<Result> Handle(AssignUserRolesCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users
            .Include(u => u.Roles)
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        List<Guid> existingRoleIds = await context.Roles
            .Where(r => command.RoleIds.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        Guid missingRoleId = command.RoleIds.FirstOrDefault(id => !existingRoleIds.Contains(id));
        if (missingRoleId != Guid.Empty)
        {
            return Result.Failure(RoleErrors.NotFound(missingRoleId));
        }

        user.SetRoles(command.RoleIds);

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(PermissionCacheKeys.ForUser(user.Id), cancellationToken);

        return Result.Success();
    }
}
