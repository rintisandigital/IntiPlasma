using FluentValidation;

namespace Application.Uoms.Update;

internal sealed class UpdateUomCommandValidator : AbstractValidator<UpdateUomCommand>
{
    public UpdateUomCommandValidator()
    {
        RuleFor(c => c.UomId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(50);
    }
}
