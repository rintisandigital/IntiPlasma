using FluentValidation;

namespace Application.TaxCodes;

/// <param name="RatePercent">Tariff in percent, e.g. 12.</param>
/// <param name="TaxBaseRatio">DPP fraction; 1 for a normal tax base, 11/12 for "DPP nilai lain".</param>
public sealed record TaxRateRequest(DateOnly EffectiveFrom, decimal RatePercent, decimal TaxBaseRatio);

internal sealed class TaxRateRequestValidator : AbstractValidator<TaxRateRequest>
{
    public TaxRateRequestValidator()
    {
        RuleFor(r => r.RatePercent).InclusiveBetween(0, 100);
        RuleFor(r => r.TaxBaseRatio).GreaterThan(0).LessThanOrEqualTo(1);
    }
}
