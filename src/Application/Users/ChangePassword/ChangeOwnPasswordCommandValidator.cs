using FluentValidation;

namespace Application.Users.ChangePassword;

internal sealed class ChangeOwnPasswordCommandValidator : AbstractValidator<ChangeOwnPasswordCommand>
{
    public ChangeOwnPasswordCommandValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty();
        RuleFor(c => c.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .NotEqual(c => c.CurrentPassword)
            .WithMessage("The new password must be different from the current password.");
    }
}
