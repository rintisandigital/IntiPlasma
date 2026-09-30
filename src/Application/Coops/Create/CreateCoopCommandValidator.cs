using FluentValidation;

namespace Application.Coops.Create;

internal sealed class CreateCoopCommandValidator : AbstractValidator<CreateCoopCommand>
{
    public CreateCoopCommandValidator()
    {
        RuleFor(c => c.FarmerId).NotEmpty();
        RuleFor(c => c.Code).NotEmpty().MaximumLength(25);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Capacity).GreaterThan(0);
        RuleFor(c => c.HouseType).IsInEnum();
        RuleFor(c => c.Address).MaximumLength(500);
    }
}
