using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using SharedKernel;

namespace Application.Users.Login;

internal sealed class LoginUserCommandHandler(
    IApplicationDbContext context,
    CredentialVerifier credentials,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    RefreshTokenOptions refreshTokenOptions) : ICommandHandler<LoginUserCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        Result<User> verified = await credentials.VerifyAsync(command.Email, command.Password, cancellationToken);

        if (verified.IsFailure)
        {
            // The API contract keeps reporting an unknown email and a wrong password as NotFoundByEmail.
            return Result.Failure<AccessTokensResponse>(
                verified.Error == UserErrors.InvalidCredentials ? UserErrors.NotFoundByEmail : verified.Error);
        }

        User user = verified.Value;

        if (!user.IsActive)
        {
            return await credentials.RejectAsync<AccessTokensResponse>(user, UserErrors.Inactive, cancellationToken);
        }

        string accessToken = tokenProvider.Create(user);
        string refreshToken = tokenProvider.GenerateRefreshToken();

        var refreshTokenEntity = RefreshToken.Create(
            refreshToken,
            user.Id,
            dateTimeProvider.UtcNow.AddDays(refreshTokenOptions.RefreshTokenExpirationInDays));

        context.RefreshTokens.Add(refreshTokenEntity);

        // Saves the refresh token together with the sign-in audit entry.
        await credentials.SucceedAsync(user, cancellationToken);

        return new AccessTokensResponse(accessToken, refreshToken);
    }
}
