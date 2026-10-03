using Application.Abstractions.Authentication;
using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Manage;

/// <summary>
/// Deactivates a user: sessions end immediately and API tokens can no longer be obtained or refreshed.
/// </summary>
public sealed record DeactivateUserCommand(Guid UserId) : ICommand;

public sealed record ActivateUserCommand(Guid UserId) : ICommand;

internal sealed class DeactivateUserCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    ICacheInvalidator cache) : ICommandHandler<DeactivateUserCommand>
{
    public async Task<Result> Handle(DeactivateUserCommand command, CancellationToken cancellationToken)
    {
        if (command.UserId == userContext.UserId)
        {
            return Result.Failure(UserErrors.CannotDeactivateSelf);
        }

        User? user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        Result lastAdministrator = await UserAccessRules.EnsureNotLastAdministratorAsync(context, user, cancellationToken);
        if (lastAdministrator.IsFailure)
        {
            return lastAdministrator;
        }

        user.Deactivate();

        List<RefreshToken> refreshTokens = await context.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .ToListAsync(cancellationToken);
        context.RefreshTokens.RemoveRange(refreshTokens);

        await context.SaveChangesAsync(cancellationToken);

        await UserAccessRules.InvalidateAsync(cache, user.Id, cancellationToken);

        return Result.Success();
    }
}

internal sealed class ActivateUserCommandHandler(IApplicationDbContext context, ICacheInvalidator cache)
    : ICommandHandler<ActivateUserCommand>
{
    public async Task<Result> Handle(ActivateUserCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        user.Activate();

        await context.SaveChangesAsync(cancellationToken);

        await UserAccessRules.InvalidateAsync(cache, user.Id, cancellationToken);

        return Result.Success();
    }
}
