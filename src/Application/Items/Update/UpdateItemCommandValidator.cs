using FluentValidation;

namespace Application.Items.Update;

internal sealed class UpdateItemCommandValidator : AbstractValidator<UpdateItemCommand>
{
    public UpdateItemCommandValidator()
    {
        RuleFor(c => c.ItemId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(150);
        RuleFor(c => c.Conversions).NotNull();
        RuleForEach(c => c.Conversions).SetValidator(new ItemUomConversionRequestValidator());
    }
}
