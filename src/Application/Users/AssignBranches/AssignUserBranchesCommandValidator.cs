using FluentValidation;

namespace Application.Users.AssignBranches;

internal sealed class AssignUserBranchesCommandValidator : AbstractValidator<AssignUserBranchesCommand>
{
    public AssignUserBranchesCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.BranchIds).NotNull();
        RuleForEach(c => c.BranchIds).NotEmpty();
    }
}
