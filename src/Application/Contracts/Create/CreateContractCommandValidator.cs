using Application.Documents;
using FluentValidation;

namespace Application.Contracts.Create;

internal sealed class CreateContractCommandValidator : AbstractValidator<CreateContractCommand>
{
    public CreateContractCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(30);
        RuleFor(c => c.BranchId).NotEmpty();
        RuleFor(c => c.Scheme).IsInEnum();
        RuleFor(c => c.Terms).NotNull().SetValidator(new ContractTermsRequestValidator());
        RuleFor(c => c.Documents).ValidDocuments();
    }
}
