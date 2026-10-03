using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Roles;
using Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Manage;

/// <summary>
/// Creates a user with an initial password (no forced change, W-21), menu & branch access (W-15) and optional
/// API roles (W-20) in one transaction.
/// </summary>
public sealed record CreateUserCommand(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    Guid? MenuAccessProfileId,
    Guid? BranchAccessProfileId,
    Guid? DefaultBranchId,
    IReadOnlyList<Guid> RoleIds) : ICommand<Guid>;

internal sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Password).NotEmpty().MinimumLength(8);
        RuleFor(c => c.RoleIds).NotNull();
    }
}

internal sealed class CreateUserCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    : ICommandHandler<CreateUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        string email = command.Email.Trim();

        if (await context.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        Result access = await UserAccessRules.ValidateAccessAsync(
            context, command.MenuAccessProfileId, command.BranchAccessProfileId, command.DefaultBranchId, cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        Guid missingRoleId = await FindMissingRoleAsync(command.RoleIds, cancellationToken);
        if (missingRoleId != Guid.Empty)
        {
            return Result.Failure<Guid>(RoleErrors.NotFound(missingRoleId));
        }

        var user = User.Create(email, command.FirstName.Trim(), command.LastName.Trim(), passwordHasher.Hash(command.Password));
        user.SetAccess(command.MenuAccessProfileId, command.BranchAccessProfileId, command.DefaultBranchId);
        user.SetRoles(command.RoleIds);

        context.Users.Add(user);

        await context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }

    private async Task<Guid> FindMissingRoleAsync(IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken)
    {
        List<Guid> existing = await context.Roles
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        return roleIds.FirstOrDefault(id => !existing.Contains(id));
    }
}
