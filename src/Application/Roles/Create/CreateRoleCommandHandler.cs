using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Roles.Create;

internal sealed class CreateRoleCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateRoleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        if (await context.Roles.AnyAsync(r => r.Name == command.Name, cancellationToken))
        {
            return Result.Failure<Guid>(RoleErrors.NameNotUnique);
        }

        Result<Role> result = Role.Create(command.Name, command.Description, command.Permissions);

        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        context.Roles.Add(result.Value);

        await context.SaveChangesAsync(cancellationToken);

        return result.Value.Id;
    }
}
