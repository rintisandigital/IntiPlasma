using FluentValidation;

namespace Application.Cycles.Cancel;

internal sealed class CancelCycleCommandValidator : AbstractValidator<CancelCycleCommand>
{
    public CancelCycleCommandValidator()
    {
        RuleFor(c => c.CycleId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    }
}
