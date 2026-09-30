namespace Application.Items;

public sealed record ItemResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public string Category { get; init; }

    public Guid BaseUomId { get; init; }

    public string BaseUomCode { get; init; }

    public Guid? TaxCodeId { get; init; }

    public string? TaxCode { get; init; }

    public bool IsActive { get; init; }

    public IReadOnlyList<ItemUomConversionResponse> Conversions { get; init; } = [];
}

public sealed record ItemUomConversionResponse(Guid UomId, string UomCode, decimal Factor);
