using Application.Common;
using Application.Documents;
using FluentValidation;

namespace Application.Farmers.Create;

internal sealed class CreateFarmerCommandValidator : AbstractValidator<CreateFarmerCommand>
{
    public CreateFarmerCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(30);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(150);
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.BranchId).NotEmpty();
        RuleFor(c => c.Nik).MaximumLength(30);
        RuleFor(c => c.TaxIdentity).NotNull().SetValidator(new TaxIdentityRequestValidator());
        RuleFor(c => c.BankAccount).NotNull().SetValidator(new BankAccountRequestValidator());
        RuleFor(c => c.Address).MaximumLength(500);
        RuleFor(c => c.Phone).MaximumLength(30);
        RuleFor(c => c.Documents).ValidDocuments();
    }
}
