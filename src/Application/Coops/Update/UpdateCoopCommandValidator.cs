using FluentValidation;

namespace Application.Coops.Update;

internal sealed class UpdateCoopCommandValidator : AbstractValidator<UpdateCoopCommand>
{
    public UpdateCoopCommandValidator()
    {
        RuleFor(c => c.CoopId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Capacity).GreaterThan(0);
        RuleFor(c => c.HouseType).IsInEnum();
        RuleFor(c => c.Address).MaximumLength(500);
    }
}
