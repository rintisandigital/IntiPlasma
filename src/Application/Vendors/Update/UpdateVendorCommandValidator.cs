using Application.Common;
using Application.Documents;
using FluentValidation;

namespace Application.Vendors.Update;

internal sealed class UpdateVendorCommandValidator : AbstractValidator<UpdateVendorCommand>
{
    public UpdateVendorCommandValidator()
    {
        RuleFor(c => c.VendorId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(150);
        RuleFor(c => c.TaxIdentity).NotNull().SetValidator(new TaxIdentityRequestValidator());
        RuleFor(c => c.BankAccount).NotNull().SetValidator(new BankAccountRequestValidator());
        RuleFor(c => c.Address).MaximumLength(500);
        RuleFor(c => c.Phone).MaximumLength(30);
        RuleFor(c => c.Email).MaximumLength(256).EmailAddress().When(c => !string.IsNullOrWhiteSpace(c.Email));
        RuleFor(c => c.PaymentTermDays).InclusiveBetween(0, 365);
        RuleFor(c => c.PriceTolerancePercent).InclusiveBetween(0, 100);
        RuleFor(c => c.Documents).ValidDocuments();
    }
}
