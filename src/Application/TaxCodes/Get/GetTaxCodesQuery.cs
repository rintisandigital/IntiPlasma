using Application.Abstractions.Messaging;

namespace Application.TaxCodes.Get;

public sealed record GetTaxCodesQuery : IQuery<IReadOnlyList<TaxCodeResponse>>;

public sealed record TaxCodeResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public string Type { get; init; }

    public string? VatTreatment { get; init; }

    public string? IncomeTaxArticle { get; init; }

    public bool IsActive { get; init; }

    public IReadOnlyList<TaxRateResponse> Rates { get; init; } = [];
}

public sealed record TaxRateResponse(DateOnly EffectiveFrom, decimal RatePercent, decimal TaxBaseRatio);
