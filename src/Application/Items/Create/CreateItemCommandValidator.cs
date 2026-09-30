using FluentValidation;

namespace Application.Items.Create;

internal sealed class CreateItemCommandValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(30);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(150);
        RuleFor(c => c.Category).IsInEnum();
        RuleFor(c => c.BaseUomId).NotEmpty();
        RuleFor(c => c.Conversions).NotNull();
        RuleForEach(c => c.Conversions).SetValidator(new ItemUomConversionRequestValidator());
    }
}
