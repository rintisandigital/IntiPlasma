using Domain.Partnership.Contracts;
using FluentValidation;
using SharedKernel;

namespace Application.Contracts;

public sealed record ContractTermsRequest(
    string Name,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    decimal? PlasmaProfitSharePercent,
    Guid? IncomeTaxCodeId,
    string? Notes,
    IReadOnlyList<ContractTermsRequest.InputPrice> InputPrices,
    IReadOnlyList<ContractTermsRequest.LiveBirdPrice> LiveBirdPrices,
    IReadOnlyList<ContractTermsRequest.Incentive> Incentives)
{
    public sealed record InputPrice(Guid ItemId, decimal Price);

    public sealed record LiveBirdPrice(decimal MinWeightKg, decimal MaxWeightKg, decimal PricePerKg);

    public sealed record Incentive(
        string Name,
        IncentiveKind Kind,
        IncentiveMetric Metric,
        decimal? RangeFrom,
        decimal? RangeTo,
        decimal Amount,
        IncentiveBasis Basis);

    public ContractTerms ToDomain() =>
        new(
            Name,
            ValidFrom,
            ValidTo,
            PlasmaProfitSharePercent,
            IncomeTaxCodeId,
            Notes,
            [.. InputPrices.Select(p => new ContractTerms.InputPrice(p.ItemId, new Money(p.Price)))],
            [.. LiveBirdPrices.Select(p => new ContractTerms.LiveBirdPrice(p.MinWeightKg, p.MaxWeightKg, new Money(p.PricePerKg)))],
            [.. Incentives.Select(i => new ContractTerms.Incentive(
                i.Name, i.Kind, i.Metric, i.RangeFrom, i.RangeTo, new Money(i.Amount), i.Basis))]);
}

internal sealed class ContractTermsRequestValidator : AbstractValidator<ContractTermsRequest>
{
    public ContractTermsRequestValidator()
    {
        RuleFor(t => t.Name).NotEmpty().MaximumLength(150);
        RuleFor(t => t.Notes).MaximumLength(1000);
        RuleFor(t => t.InputPrices).NotNull();
        RuleFor(t => t.LiveBirdPrices).NotNull();
        RuleFor(t => t.Incentives).NotNull();
        RuleForEach(t => t.InputPrices).ChildRules(p => p.RuleFor(x => x.ItemId).NotEmpty());
        RuleForEach(t => t.Incentives).ChildRules(i =>
        {
            i.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            i.RuleFor(x => x.Kind).IsInEnum();
            i.RuleFor(x => x.Metric).IsInEnum();
            i.RuleFor(x => x.Basis).IsInEnum();
        });
    }
}
