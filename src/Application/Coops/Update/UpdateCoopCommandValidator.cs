using Application.Documents;
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
        RuleFor(c => c.Documents).ValidDocuments();
        RuleFor(c => c.Profile!).SetValidator(new CoopProfileValidator()).When(c => c.Profile is not null);
    }
}
