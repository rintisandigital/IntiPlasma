using Application.Abstractions.Authentication;
using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Manage;

/// <summary>
/// Deletes a user (W-22) after copying it to the backup table <c>identity.user_old</c> in the same transaction.
/// Roles and refresh tokens go with the user (cascade); documents keep the user id in their audit columns.
/// </summary>
public sealed record DeleteUserCommand(Guid UserId, string? Reason) : ICommand;

internal sealed class DeleteUserCommandValidator : AbstractValidator<DeleteUserCommand>
{
    public DeleteUserCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Reason).MaximumLength(500);
    }
}

internal sealed class DeleteUserCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider,
    ICacheInvalidator cache) : ICommandHandler<DeleteUserCommand>
{
    public async Task<Result> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        Guid currentUserId = userContext.UserId;

        if (command.UserId == currentUserId)
        {
            return Result.Failure(UserErrors.CannotDeleteSelf);
        }

        User? user = await context.Users
            .Include(u => u.Roles)
            .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        Result lastAdministrator = await UserAccessRules.EnsureNotLastAdministratorAsync(context, user, cancellationToken);
        if (lastAdministrator.IsFailure)
        {
            return lastAdministrator;
        }

        var roleIds = user.Roles.Select(r => r.RoleId).ToList();
        List<(Guid Id, string Name)> roles = [.. (await context.Roles
                .Where(r => roleIds.Contains(r.Id))
                .OrderBy(r => r.Name)
                .Select(r => new { r.Id, r.Name })
                .ToListAsync(cancellationToken))
            .Select(r => (r.Id, r.Name))];

        context.DeletedUsers.Add(DeletedUser.From(user, roles, dateTimeProvider.UtcNow, currentUserId, command.Reason));

        List<RefreshToken> refreshTokens = await context.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .ToListAsync(cancellationToken);
        context.RefreshTokens.RemoveRange(refreshTokens);

        context.Users.Remove(user);

        // One SaveChanges = one transaction: the backup row exists if and only if the user is gone.
        await context.SaveChangesAsync(cancellationToken);

        await UserAccessRules.InvalidateAsync(cache, user.Id, cancellationToken);

        return Result.Success();
    }
}
