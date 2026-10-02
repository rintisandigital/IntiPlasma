using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.ChangePassword;

internal sealed class ChangeOwnPasswordCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPasswordHasher passwordHasher,
    ICacheInvalidator cache) : ICommandHandler<ChangeOwnPasswordCommand>
{
    public async Task<Result> Handle(ChangeOwnPasswordCommand command, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;

        User? user = await context.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(userId));
        }

        if (!passwordHasher.Verify(command.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure(UserErrors.InvalidCurrentPassword);
        }

        user.ChangePassword(passwordHasher.Hash(command.NewPassword));

        // API sessions (mobile) must sign in again with the new password.
        List<RefreshToken> refreshTokens = await context.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ToListAsync(cancellationToken);

        context.RefreshTokens.RemoveRange(refreshTokens);

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(PermissionCacheKeys.SessionForUser(userId), cancellationToken);

        return Result.Success();
    }
}
