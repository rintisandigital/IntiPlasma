namespace Application.Contracts;

public sealed record ContractResponse
{
    /// <summary>
    /// Lampiran: attachment ids; metadata via <c>GET /attachments?ids=</c>.
    /// </summary>
    public Guid[] Documents { get; init; } = [];

    public const string Select =
        """
        SELECT c.id AS Id, c.code AS Code, c.name AS Name, c.branch_id AS BranchId, b.code AS BranchCode,
               c.scheme AS Scheme, c.status AS Status, c.valid_from AS ValidFrom, c.valid_to AS ValidTo,
               c.plasma_profit_share_percent AS PlasmaProfitSharePercent, c.income_tax_code_id AS IncomeTaxCodeId,
               t.code AS IncomeTaxCode, c.notes AS Notes,
               c.documents AS Documents
        FROM partnership.contracts c
        JOIN master.branches b ON b.id = c.branch_id
        LEFT JOIN master.tax_codes t ON t.id = c.income_tax_code_id
        """;

    public Guid Id { get; init; }

    public string Code { get; init; }

    public string Name { get; init; }

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; }

    public string Scheme { get; init; }

    public string Status { get; init; }

    public DateOnly ValidFrom { get; init; }

    public DateOnly? ValidTo { get; init; }

    public decimal? PlasmaProfitSharePercent { get; init; }

    public Guid? IncomeTaxCodeId { get; init; }

    public string? IncomeTaxCode { get; init; }

    public string? Notes { get; init; }

    /// <summary>
    /// Only filled by the detail endpoint.
    /// </summary>
    public IReadOnlyList<InputPriceResponse>? InputPrices { get; init; }

    public IReadOnlyList<LiveBirdPriceResponse>? LiveBirdPrices { get; init; }

    public IReadOnlyList<IncentiveResponse>? Incentives { get; init; }

    public sealed record InputPriceResponse(Guid ItemId, string ItemCode, string ItemName, string UomCode, decimal Price);

    public sealed record LiveBirdPriceResponse(decimal MinWeightKg, decimal MaxWeightKg, decimal PricePerKg);

    public sealed record IncentiveResponse(
        string Name,
        string Kind,
        string Metric,
        decimal? RangeFrom,
        decimal? RangeTo,
        decimal Amount,
        string Basis);
}
