using Domain.MasterData.TaxCodes;
using FluentValidation;

namespace Application.TaxCodes.Create;

internal sealed class CreateTaxCodeCommandValidator : AbstractValidator<CreateTaxCodeCommand>
{
    public CreateTaxCodeCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.VatTreatment).NotNull().IsInEnum().When(c => c.Type == TaxType.Vat);
        RuleFor(c => c.IncomeTaxArticle).NotNull().IsInEnum().When(c => c.Type == TaxType.IncomeTax);
        RuleFor(c => c.Rates).NotEmpty();
        RuleForEach(c => c.Rates).SetValidator(new TaxRateRequestValidator());
    }
}
