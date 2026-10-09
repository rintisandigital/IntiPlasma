namespace MobileApp.Core.Contracts;

/// <summary>
/// A page of a Web.Api list (<c>{ items, page, pageSize, totalCount }</c>).
/// </summary>
public sealed record PagedList<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public long TotalCount { get; init; }

    public bool HasMore => (long)Page * PageSize < TotalCount;
}

/// <summary>
/// <c>GET farmers</c>, <c>GET farmers/{id}</c>.
/// </summary>
public sealed record Farmer
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// <c>Inti</c> or <c>Plasma</c>.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; } = string.Empty;

    public string? Nik { get; init; }

    public TaxIdentity TaxIdentity { get; init; } = new();

    public string? Address { get; init; }

    public string? Phone { get; init; }

    public BankAccount BankAccount { get; init; } = new();

    public int CoopCount { get; init; }

    public bool IsActive { get; init; }

    public IReadOnlyList<Guid> Documents { get; init; } = [];

    public Guid? FieldOfficerUserId { get; init; }

    public string? FieldOfficerName { get; init; }
}

public sealed record TaxIdentity
{
    public string? Npwp { get; init; }

    public string? Nitku { get; init; }

    public bool IsPkp { get; init; }
}

public sealed record BankAccount
{
    public string? BankName { get; init; }

    public string? AccountNumber { get; init; }

    public string? AccountHolderName { get; init; }
}

/// <summary>
/// Kandang: <c>GET coops</c>, <c>GET coops/{id}</c>. The survey profile is not used by the app yet.
/// </summary>
public sealed record Coop
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public Guid FarmerId { get; init; }

    public string FarmerCode { get; init; } = string.Empty;

    public string FarmerName { get; init; } = string.Empty;

    public string FarmerType { get; init; } = string.Empty;

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; } = string.Empty;

    public int Capacity { get; init; }

    public string HouseType { get; init; } = string.Empty;

    public string? Address { get; init; }

    public decimal? Latitude { get; init; }

    public decimal? Longitude { get; init; }

    public bool IsActive { get; init; }

    public Guid? OpenCycleId { get; init; }

    public Guid? WarehouseId { get; init; }

    public IReadOnlyList<Guid> Documents { get; init; } = [];

    public Guid? FieldOfficerUserId { get; init; }

    public string? FieldOfficerName { get; init; }

    public bool HasLocation => Latitude is not null && Longitude is not null;
}

/// <summary>
/// Kontrak kemitraan: <c>GET contracts</c>; the price lists are only filled by <c>GET contracts/{id}</c>.
/// </summary>
public sealed record Contract
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string BranchCode { get; init; } = string.Empty;

    /// <summary>
    /// <c>PriceContract</c> or <c>ProfitSharing</c>.
    /// </summary>
    public string Scheme { get; init; } = string.Empty;

    /// <summary>
    /// <c>Draft</c>, <c>Active</c> or <c>Inactive</c>.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    public DateOnly ValidFrom { get; init; }

    public DateOnly? ValidTo { get; init; }

    public decimal? PlasmaProfitSharePercent { get; init; }

    public string? IncomeTaxCode { get; init; }

    public string? Notes { get; init; }

    public IReadOnlyList<Guid> Documents { get; init; } = [];

    public IReadOnlyList<ContractInputPrice>? InputPrices { get; init; }

    public IReadOnlyList<ContractLiveBirdPrice>? LiveBirdPrices { get; init; }

    public IReadOnlyList<ContractIncentive>? Incentives { get; init; }
}

public sealed record ContractInputPrice(Guid ItemId, string ItemCode, string ItemName, string UomCode, decimal Price);

public sealed record ContractLiveBirdPrice(decimal MinWeightKg, decimal MaxWeightKg, decimal PricePerKg);

public sealed record ContractIncentive(
    string Name,
    string Kind,
    string Metric,
    decimal? RangeFrom,
    decimal? RangeTo,
    decimal Amount,
    string Basis);

/// <summary>
/// Siklus produksi: <c>GET cycles</c>.
/// </summary>
public sealed record Cycle
{
    public Guid Id { get; init; }

    public string Number { get; init; } = string.Empty;

    /// <summary>
    /// <c>Planned</c>, <c>Active</c>, <c>Harvesting</c>, <c>Closed</c>, <c>Settled</c> or <c>Cancelled</c>.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    public Guid CoopId { get; init; }

    public string CoopCode { get; init; } = string.Empty;

    public string CoopName { get; init; } = string.Empty;

    public string? ContractCode { get; init; }

    public DateOnly PlannedChickInDate { get; init; }

    public int PlannedPopulation { get; init; }

    public DateOnly? ChickInDate { get; init; }

    public int? InitialPopulation { get; init; }

    public int TotalMortality { get; init; }

    public int TotalCulling { get; init; }

    public int HarvestedBirds { get; init; }

    public decimal HarvestedWeightKg { get; init; }

    public int CurrentPopulation { get; init; }

    public DateOnly? ClosedDate { get; init; }
}

/// <summary>
/// Saldo stok per item of a warehouse: <c>GET inventory/stock-balances?warehouseId=</c>.
/// </summary>
public sealed record StockBalance(
    Guid WarehouseId,
    string WarehouseCode,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string ItemCategory,
    string BaseUomCode,
    decimal Quantity);

/// <summary>
/// Lampiran metadata: <c>GET attachments?ids=</c>.
/// </summary>
public sealed record Attachment
{
    public Guid Id { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    /// <summary>
    /// <c>Photo</c> or <c>Document</c>.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsPhoto => Kind == "Photo";
}

/// <summary>
/// Downloaded attachment file.
/// </summary>
public sealed record AttachmentContent(string ContentType, byte[] Bytes);

/// <summary>
/// PPL of a branch: <c>GET users/field-officers</c>.
/// </summary>
public sealed record FieldOfficer
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}
