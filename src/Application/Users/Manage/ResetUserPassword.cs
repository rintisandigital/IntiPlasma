using Application.Abstractions.Auditing;
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
/// The administrator sets a new password (no forced change on next sign-in, W-21). Existing sessions and API
/// refresh tokens of the user stop working.
/// </summary>
public sealed record ResetUserPasswordCommand(Guid UserId, string NewPassword) : ICommand, IAuditedCommand
{
    string IAuditedCommand.AuditEntityType => "User";
}

internal sealed class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.NewPassword).NotEmpty().MinimumLength(8);
    }
}

internal sealed class ResetUserPasswordCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ICacheInvalidator cache) : ICommandHandler<ResetUserPasswordCommand>
{
    public async Task<Result> Handle(ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        user.ChangePassword(passwordHasher.Hash(command.NewPassword));

        List<RefreshToken> refreshTokens = await context.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .ToListAsync(cancellationToken);
        context.RefreshTokens.RemoveRange(refreshTokens);

        await context.SaveChangesAsync(cancellationToken);

        await UserAccessRules.InvalidateAsync(cache, user.Id, cancellationToken);

        return Result.Success();
    }
}
