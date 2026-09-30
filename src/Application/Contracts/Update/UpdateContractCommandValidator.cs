using FluentValidation;

namespace Application.Contracts.Update;

internal sealed class UpdateContractCommandValidator : AbstractValidator<UpdateContractCommand>
{
    public UpdateContractCommandValidator()
    {
        RuleFor(c => c.ContractId).NotEmpty();
        RuleFor(c => c.Terms).NotNull().SetValidator(new ContractTermsRequestValidator());
    }
}
