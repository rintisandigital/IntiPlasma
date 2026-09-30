using Application.Common;
using FluentValidation;

namespace Application.Farmers.Update;

internal sealed class UpdateFarmerCommandValidator : AbstractValidator<UpdateFarmerCommand>
{
    public UpdateFarmerCommandValidator()
    {
        RuleFor(c => c.FarmerId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(150);
        RuleFor(c => c.Nik).MaximumLength(30);
        RuleFor(c => c.TaxIdentity).NotNull().SetValidator(new TaxIdentityRequestValidator());
        RuleFor(c => c.BankAccount).NotNull().SetValidator(new BankAccountRequestValidator());
        RuleFor(c => c.Address).MaximumLength(500);
        RuleFor(c => c.Phone).MaximumLength(30);
    }
}
