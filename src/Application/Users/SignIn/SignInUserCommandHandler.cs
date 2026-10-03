using Application.Abstractions.Messaging;
using Domain.Users;
using SharedKernel;

namespace Application.Users.SignIn;

internal sealed class SignInUserCommandHandler(CredentialVerifier credentials)
    : ICommandHandler<SignInUserCommand, SignedInUserResponse>
{
    public async Task<Result<SignedInUserResponse>> Handle(SignInUserCommand command, CancellationToken cancellationToken)
    {
        // Same error for an unknown email and a wrong password, so the login form does not reveal accounts.
        Result<User> verified = await credentials.VerifyAsync(command.Email, command.Password, cancellationToken);

        if (verified.IsFailure)
        {
            return Result.Failure<SignedInUserResponse>(verified.Error);
        }

        User user = verified.Value;

        if (!user.IsActive)
        {
            return await credentials.RejectAsync<SignedInUserResponse>(user, UserErrors.Inactive, cancellationToken);
        }

        // Web.App shows nothing without a menu access profile (the API does not need one).
        if (user.MenuAccessProfileId is null)
        {
            return await credentials.RejectAsync<SignedInUserResponse>(user, UserErrors.NoMenuAccess, cancellationToken);
        }

        await credentials.SucceedAsync(user, cancellationToken);

        return new SignedInUserResponse(user.Id, user.Email, user.FirstName, user.LastName, user.SecurityStamp);
    }
}
