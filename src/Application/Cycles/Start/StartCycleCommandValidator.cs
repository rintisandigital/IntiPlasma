using FluentValidation;

namespace Application.Cycles.Start;

internal sealed class StartCycleCommandValidator : AbstractValidator<StartCycleCommand>
{
    public StartCycleCommandValidator()
    {
        RuleFor(c => c.CycleId).NotEmpty();
        RuleFor(c => c.InitialPopulation).GreaterThan(0);
    }
}
