using FluentValidation;

namespace Application.TaxCodes.Update;

internal sealed class UpdateTaxCodeCommandValidator : AbstractValidator<UpdateTaxCodeCommand>
{
    public UpdateTaxCodeCommandValidator()
    {
        RuleFor(c => c.TaxCodeId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Rates).NotEmpty();
        RuleForEach(c => c.Rates).SetValidator(new TaxRateRequestValidator());
    }
}
