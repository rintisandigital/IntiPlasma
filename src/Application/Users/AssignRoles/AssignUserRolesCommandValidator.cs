using FluentValidation;

namespace Application.Users.AssignRoles;

internal sealed class AssignUserRolesCommandValidator : AbstractValidator<AssignUserRolesCommand>
{
    public AssignUserRolesCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.RoleIds).NotNull();
        RuleForEach(c => c.RoleIds).NotEmpty();
    }
}
