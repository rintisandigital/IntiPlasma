using FluentValidation;

namespace Application.Users.SignIn;

internal sealed class SignInUserCommandValidator : AbstractValidator<SignInUserCommand>
{
    public SignInUserCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Password).NotEmpty();
    }
}
