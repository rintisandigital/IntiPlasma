using Application.Common;
using FluentValidation;

namespace Application.Customers.Create;

internal sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(30);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(150);
        RuleFor(c => c.TaxIdentity).NotNull().SetValidator(new TaxIdentityRequestValidator());
        RuleFor(c => c.Address).MaximumLength(500);
        RuleFor(c => c.Phone).MaximumLength(30);
        RuleFor(c => c.Email).MaximumLength(256).EmailAddress().When(c => !string.IsNullOrWhiteSpace(c.Email));
        RuleFor(c => c.PaymentTermDays).InclusiveBetween(0, 365);
        RuleFor(c => c.CreditLimit).GreaterThanOrEqualTo(0);
    }
}
