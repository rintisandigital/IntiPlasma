using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Refresh;

internal sealed class RefreshTokenCommandHandler(
    IApplicationDbContext context,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    RefreshTokenOptions refreshTokenOptions) : ICommandHandler<RefreshTokenCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        RefreshToken? refreshToken = await context.RefreshTokens
            .Include(rt => rt.User)
            .SingleOrDefaultAsync(rt => rt.Token == command.RefreshToken, cancellationToken);

        if (refreshToken is null || refreshToken.IsExpired(dateTimeProvider.UtcNow))
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        if (!refreshToken.User.IsActive)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.Inactive);
        }

        string accessToken = tokenProvider.Create(refreshToken.User);
        string newRefreshToken = tokenProvider.GenerateRefreshToken();

        refreshToken.Rotate(newRefreshToken, dateTimeProvider.UtcNow.AddDays(refreshTokenOptions.RefreshTokenExpirationInDays));

        await context.SaveChangesAsync(cancellationToken);

        return new AccessTokensResponse(accessToken, newRefreshToken);
    }
}
