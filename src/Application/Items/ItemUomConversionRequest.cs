using FluentValidation;

namespace Application.Items;

/// <param name="Factor">Base units in one <paramref name="UomId"/>, e.g. 50 for SAK → KG.</param>
public sealed record ItemUomConversionRequest(Guid UomId, decimal Factor);

internal sealed class ItemUomConversionRequestValidator : AbstractValidator<ItemUomConversionRequest>
{
    public ItemUomConversionRequestValidator()
    {
        RuleFor(c => c.UomId).NotEmpty();
        RuleFor(c => c.Factor).GreaterThan(0);
    }
}
