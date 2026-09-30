using FluentValidation;

namespace Application.Branches.Create;

internal sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        // The code is part of every document number, so it must stay short and URL/filename friendly.
        RuleFor(c => c.Code).NotEmpty().MaximumLength(10).Matches("^[A-Za-z0-9]+$");
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Address).MaximumLength(500);
        RuleFor(c => c.Phone).MaximumLength(30);
    }
}
