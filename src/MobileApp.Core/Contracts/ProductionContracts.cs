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
