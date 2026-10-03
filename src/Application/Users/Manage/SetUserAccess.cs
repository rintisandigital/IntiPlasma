using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Manage;

/// <summary>
/// Assigns the user's menu access profile, branch access profile and default branch. Takes effect immediately
/// in Web.Api and Web.App (cached access is invalidated in every process).
/// </summary>
public sealed record SetUserAccessCommand(
    Guid UserId,
    Guid? MenuAccessProfileId,
    Guid? BranchAccessProfileId,
    Guid? DefaultBranchId) : ICommand;

internal sealed class SetUserAccessCommandValidator : AbstractValidator<SetUserAccessCommand>
{
    public SetUserAccessCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}

internal sealed class SetUserAccessCommandHandler(IApplicationDbContext context, ICacheInvalidator cache)
    : ICommandHandler<SetUserAccessCommand>
{
    public async Task<Result> Handle(SetUserAccessCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        Result access = await UserAccessRules.ValidateAccessAsync(
            context, command.MenuAccessProfileId, command.BranchAccessProfileId, command.DefaultBranchId, cancellationToken);

        if (access.IsFailure)
        {
            return access;
        }

        if (command.MenuAccessProfileId != user.MenuAccessProfileId)
        {
            Result lastAdministrator = await UserAccessRules.EnsureNotLastAdministratorAsync(context, user, cancellationToken);
            if (lastAdministrator.IsFailure)
            {
                return lastAdministrator;
            }
        }

        user.SetAccess(command.MenuAccessProfileId, command.BranchAccessProfileId, command.DefaultBranchId);

        await context.SaveChangesAsync(cancellationToken);

        await UserAccessRules.InvalidateAsync(cache, user.Id, cancellationToken);

        return Result.Success();
    }
}
