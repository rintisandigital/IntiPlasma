using FluentValidation;

namespace Application.Cycles.Plan;

internal sealed class PlanCycleCommandValidator : AbstractValidator<PlanCycleCommand>
{
    public PlanCycleCommandValidator()
    {
        RuleFor(c => c.CoopId).NotEmpty();
        RuleFor(c => c.PlannedPopulation).GreaterThan(0);
        RuleFor(c => c.Notes).MaximumLength(1000);
    }
}
