using Application.Documents;
using FluentValidation;

namespace Application.Cycles.Start;

internal sealed class StartCycleCommandValidator : AbstractValidator<StartCycleCommand>
{
    public StartCycleCommandValidator()
    {
        RuleFor(c => c.CycleId).NotEmpty();
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
        });
        RuleFor(c => c.Documents).ValidDocuments();
    }
}
