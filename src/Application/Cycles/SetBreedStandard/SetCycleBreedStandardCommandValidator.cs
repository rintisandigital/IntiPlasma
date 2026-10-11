using FluentValidation;

namespace Application.Cycles.SetBreedStandard;

internal sealed class SetCycleBreedStandardCommandValidator : AbstractValidator<SetCycleBreedStandardCommand>
{
    public SetCycleBreedStandardCommandValidator()
    {
        RuleFor(c => c.CycleId).NotEmpty();
        RuleFor(c => c.BreedStandardId).NotEqual(Guid.Empty);
    }
}
