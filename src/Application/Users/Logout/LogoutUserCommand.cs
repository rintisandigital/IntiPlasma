using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Logout;

/// <summary>
/// Ends one API session (a mobile device): its refresh token stops working. The access token stays valid until it
/// expires, so the client also discards it. Unknown tokens and tokens of another user are ignored (idempotent).
/// </summary>
public sealed record LogoutUserCommand(string RefreshToken) : ICommand;

internal sealed class LogoutUserCommandValidator : AbstractValidator<LogoutUserCommand>
{
    public LogoutUserCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty();
    }
}

internal sealed class LogoutUserCommandHandler(IApplicationDbContext context, IUserContext userContext)
    : ICommandHandler<LogoutUserCommand>
{
    public async Task<Result> Handle(LogoutUserCommand command, CancellationToken cancellationToken)
    {
        RefreshToken? refreshToken = await context.RefreshTokens.SingleOrDefaultAsync(
            rt => rt.Token == command.RefreshToken && rt.UserId == userContext.UserId,
            cancellationToken);

        if (refreshToken is null)
        {
            return Result.Success();
        }

        context.RefreshTokens.Remove(refreshToken);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
