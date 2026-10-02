using Application.Abstractions.Authorization;
using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Roles.Update;

internal sealed class UpdateRoleCommandHandler(IApplicationDbContext context, ICacheInvalidator cache)
    : ICommandHandler<UpdateRoleCommand>
{
    public async Task<Result> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        Role? role = await context.Roles
            .Include(r => r.Permissions)
            .SingleOrDefaultAsync(r => r.Id == command.RoleId, cancellationToken);

        if (role is null)
        {
            return Result.Failure(RoleErrors.NotFound(command.RoleId));
        }

        if (await context.Roles.AnyAsync(r => r.Id != command.RoleId && r.Name == command.Name, cancellationToken))
        {
            return Result.Failure(RoleErrors.NameNotUnique);
        }

        Result result = role.Update(command.Name, command.Description, command.Permissions);

        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveByTagAsync(PermissionCacheKeys.Tag, cancellationToken);

        return Result.Success();
    }
}
