using FluentValidation;

namespace Application.Uoms.Create;

internal sealed class CreateUomCommandValidator : AbstractValidator<CreateUomCommand>
{
    public CreateUomCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(10);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(50);
    }
}
