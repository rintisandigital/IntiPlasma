namespace MobileApp.Core.Contracts;

/// <summary>
/// <c>GET mobile/field-context</c> (PLAN-MOBILE M-40): what the daily forms need offline.
/// </summary>
public sealed record FieldContext
{
    /// <summary>
    /// The server's date; a recording may be at most one day after it.
    /// </summary>
    public DateOnly ServerDate { get; init; }

    public DateTime GeneratedAtUtc { get; init; }

    public IReadOnlyList<FieldCycle> Cycles { get; init; } = [];

    public IReadOnlyList<FieldItem> Items { get; init; } = [];

    public IReadOnlyList<FieldStock> Stock { get; init; } = [];

    /// <summary>
    /// Active weight ranges for stok ayam harian, in display order (M-52).
    /// </summary>
    public IReadOnlyList<WeightRange> WeightRanges { get; init; } = [];

    public WeightRange? FindRange(Guid weightRangeId) => WeightRanges.FirstOrDefault(r => r.Id == weightRangeId);

    public FieldCycle? FindCycle(Guid cycleId) => Cycles.FirstOrDefault(c => c.Id == cycleId);

    public FieldItem? FindItem(Guid itemId) => Items.FirstOrDefault(i => i.Id == itemId);

    /// <summary>
    /// Quantity in stock (base unit) of an item in a warehouse according to the server.
    /// </summary>
    public decimal StockOf(Guid? warehouseId, Guid itemId) =>
        warehouseId is null ? 0 : Stock.Where(s => s.WarehouseId == warehouseId && s.ItemId == itemId).Sum(s => s.Quantity);
}

/// <summary>
/// A running cycle (Active or Harvesting) the user may record for.
/// </summary>
public sealed record FieldCycle
{
    public Guid Id { get; init; }

    public string Number { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public Guid BranchId { get; init; }

    public Guid CoopId { get; init; }

    public string CoopCode { get; init; } = string.Empty;

    public string CoopName { get; init; } = string.Empty;

    public Guid FarmerId { get; init; }

    public string FarmerName { get; init; } = string.Empty;

    /// <summary>
    /// Gudang kandang; null when the coop has none (usage cannot be recorded).
    /// </summary>
    public Guid? WarehouseId { get; init; }

    public DateOnly ChickInDate { get; init; }

    public int InitialPopulation { get; init; }

    public int CurrentPopulation { get; init; }

    public DateOnly? LastRecordingDate { get; init; }

    /// <summary>
    /// Dates already recorded on the server within the last 14 days.
    /// </summary>
    public IReadOnlyList<DateOnly> RecordedDates { get; init; } = [];

    /// <summary>
    /// Date of the latest stok ayam report on the server, if any.
    /// </summary>
    public DateOnly? LatestStockDate { get; init; }

    /// <summary>
    /// The server entries of <see cref="LatestStockDate"/>.
    /// </summary>
    public IReadOnlyList<FieldStockEntry> LatestStock { get; init; } = [];

    public int AgeOn(DateOnly date) => date.DayNumber - ChickInDate.DayNumber;
}

/// <param name="Category"><c>Feed</c> or <c>Ovk</c>.</param>
/// <param name="Uoms">The base unit first (factor 1), then the conversions.</param>
public sealed record FieldItem(
    Guid Id,
    string Code,
    string Name,
    string Category,
    Guid BaseUomId,
    string BaseUomCode,
    IReadOnlyList<FieldUom> Uoms)
{
    public bool IsFeed => Category == "Feed";

    /// <summary>
    /// The quantity in the base unit, or null when the unit does not belong to the item.
    /// </summary>
    public decimal? ToBase(Guid uomId, decimal quantity) =>
        Uoms.FirstOrDefault(u => u.UomId == uomId) is { } uom ? quantity * uom.Factor : null;
}

/// <param name="Factor">Base units per one of this unit.</param>
public sealed record FieldUom(Guid UomId, string Code, decimal Factor);

/// <param name="Quantity">In the item's base unit.</param>
public sealed record FieldStock(Guid WarehouseId, Guid ItemId, decimal Quantity);

/// <summary>
/// A daily recording from <c>GET production/daily-recordings</c> (list) or <c>…/{id}</c> (detail with usages and
/// revisions).
/// </summary>
public sealed record DailyRecording
{
    public Guid Id { get; init; }

    public Guid CycleId { get; init; }

    public Guid BranchId { get; init; }

    public DateOnly Date { get; init; }

    public int AgeDays { get; init; }

    public int Mortality { get; init; }

    public int Culling { get; init; }

    public decimal? AverageBodyWeightGram { get; init; }

    /// <summary>
    /// Feed used that day in kg.
    /// </summary>
    public decimal FeedKg { get; init; }

    public string? Notes { get; init; }

    public int RevisionNumber { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public IReadOnlyList<Guid> Documents { get; init; } = [];

    public IReadOnlyList<RecordingUsage>? Usages { get; init; }

    public IReadOnlyList<RecordingRevision>? Revisions { get; init; }
}

public sealed record RecordingUsage(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string ItemCategory,
    string UomCode,
    decimal Quantity,
    decimal BaseQuantity,
    string BaseUomCode);

public sealed record RecordingRevision(int RevisionNumber, string Reason, DateTime RevisedAtUtc, IReadOnlyList<Guid> Documents);

/// <summary>
/// Body of <c>POST production/daily-recordings</c>; <see cref="Id"/> comes from the app so a resend is harmless.
/// </summary>
public sealed record CreateDailyRecordingRequest(
    Guid Id,
    Guid CycleId,
    DateOnly Date,
    int Mortality,
    int Culling,
    decimal? AverageBodyWeightGram,
    string? Notes,
    IReadOnlyList<UsageInput> Usages,
    IReadOnlyList<Guid> Documents);

public sealed record UsageInput(Guid ItemId, Guid UomId, decimal Quantity);

/// <summary>
/// Rentang bobot: average weight band [<see cref="MinWeightKg"/>, <see cref="MaxWeightKg"/>) in kg.
/// </summary>
public sealed record WeightRange(Guid Id, string Code, string Name, decimal? MinWeightKg, decimal? MaxWeightKg)
{
    public bool Contains(decimal averageWeightKg) =>
        (MinWeightKg is not { } min || averageWeightKg >= min) && (MaxWeightKg is not { } max || averageWeightKg < max);
}

public sealed record FieldStockEntry(Guid Id, Guid WeightRangeId, int Birds, decimal WeightKg, string? Notes);

/// <summary>
/// An entry of stok ayam harian from <c>GET production/live-bird-stocks</c>.
/// </summary>
public sealed record LiveBirdStockEntry
{
    public Guid Id { get; init; }

    public Guid CycleId { get; init; }

    public string CycleNumber { get; init; } = string.Empty;

    public Guid CoopId { get; init; }

    public string CoopName { get; init; } = string.Empty;

    public DateOnly Date { get; init; }

    public int AgeDays { get; init; }

    public Guid WeightRangeId { get; init; }

    public string WeightRangeCode { get; init; } = string.Empty;

    public string WeightRangeName { get; init; } = string.Empty;

    public int Birds { get; init; }

    public decimal WeightKg { get; init; }

    public decimal AverageWeightKg { get; init; }

    public string? Notes { get; init; }
}

/// <summary>
/// Body of <c>POST production/live-bird-stocks</c>: the same cycle, date and range again changes that entry.
/// </summary>
public sealed record UpsertLiveBirdStockRequest(
    Guid Id,
    Guid CycleId,
    DateOnly Date,
    Guid WeightRangeId,
    int Birds,
    decimal WeightKg,
    string? Notes);

/// <summary>
/// <c>GET production/live-bird-stocks/summary</c>: rekap cabang (M-51).
/// </summary>
public sealed record LiveBirdStockSummary
{
    public DateOnly Date { get; init; }

    public IReadOnlyList<LiveBirdStockRangeTotal> Ranges { get; init; } = [];

    public IReadOnlyList<LiveBirdStockCoop> Coops { get; init; } = [];

    public int TotalBirds { get; init; }

    public decimal TotalWeightKg { get; init; }

    public int ReportedCoops { get; init; }
}

public sealed record LiveBirdStockRangeTotal(
    Guid WeightRangeId,
    string Code,
    string Name,
    int Birds,
    decimal WeightKg,
    int Coops,
    decimal AverageWeightKg);

public sealed record LiveBirdStockCoop
{
    public Guid CycleId { get; init; }

    public string CycleNumber { get; init; } = string.Empty;

    public string BranchCode { get; init; } = string.Empty;

    public Guid CoopId { get; init; }

    public string CoopCode { get; init; } = string.Empty;

    public string CoopName { get; init; } = string.Empty;

    public string FarmerName { get; init; } = string.Empty;

    public string? FieldOfficerName { get; init; }

    public int AgeDays { get; init; }

    public int Population { get; init; }

    public DateOnly? ReportDate { get; init; }

    public int? DataAgeDays { get; init; }

    public bool IsStale { get; init; }

    public IReadOnlyList<LiveBirdStockCoopRange> Entries { get; init; } = [];

    public int TotalBirds { get; init; }

    public decimal TotalWeightKg { get; init; }
}

public sealed record LiveBirdStockCoopRange(Guid WeightRangeId, string Code, int Birds, decimal WeightKg, decimal AverageWeightKg);
