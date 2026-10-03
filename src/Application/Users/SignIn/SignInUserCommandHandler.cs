using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.SignIn;

internal sealed class SignInUserCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    : ICommandHandler<SignInUserCommand, SignedInUserResponse>
{
    public async Task<Result<SignedInUserResponse>> Handle(SignInUserCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == command.Email, cancellationToken);

        // Same error for an unknown email and a wrong password, so the login form does not reveal accounts.
        if (user is null || !passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            return Result.Failure<SignedInUserResponse>(UserErrors.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return Result.Failure<SignedInUserResponse>(UserErrors.Inactive);
        }

        // Web.App shows nothing without a menu access profile (the API does not need one).
        if (user.MenuAccessProfileId is null)
        {
            return Result.Failure<SignedInUserResponse>(UserErrors.NoMenuAccess);
        }

        return new SignedInUserResponse(user.Id, user.Email, user.FirstName, user.LastName, user.SecurityStamp);
    }
}
