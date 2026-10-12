namespace MobileApp.Core.Contracts;

/// <summary>
/// <c>GET mobile/dashboard</c> (PLAN-MOBILE M-55): the running cycles in the user's scope and their combined figures.
/// </summary>
public sealed record MobileDashboard
{
    /// <summary>
    /// The reference date (Asia/Jakarta) the "today" flags are about.
    /// </summary>
    public DateOnly Date { get; init; }

    public DateTime GeneratedAtUtc { get; init; }

    public DashboardThresholds Thresholds { get; init; } = new(3, 1, 5, 21);

    public DashboardKpi Kpi { get; init; } = new();

    public IReadOnlyList<DashboardCycle> Cycles { get; init; } = [];

    /// <summary>
    /// Requests waiting for the user and the user's own submissions (M-93).
    /// </summary>
    public DashboardApprovals Approvals { get; init; } = new();
}

public sealed record DashboardThresholds(int FeedWarningDays, int RecordingLateDays, decimal DepletionWarningPercent, int StockReportFromAgeDays);

public sealed record DashboardKpi
{
    public int ActiveCycles { get; init; }

    public int InitialPopulation { get; init; }

    public int Population { get; init; }

    public decimal DepletionPercent { get; init; }

    public decimal? Fcr { get; init; }

    public decimal? Ip { get; init; }

    public int RecordedToday { get; init; }

    /// <summary>
    /// Cycles old enough to report stok ayam daily.
    /// </summary>
    public int StockReportExpected { get; init; }

    public int StockReportedToday { get; init; }

    public int CyclesAtRisk { get; init; }

    public decimal FeedStockKg { get; init; }

    /// <summary>
    /// Cycles flagged <see cref="DashboardFlags.BelowStandard"/> (M-77).
    /// </summary>
    public int CyclesBelowStandard { get; init; }
}

public sealed record DashboardCycle
{
    public Guid CycleId { get; init; }

    public string Number { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public Guid BranchId { get; init; }

    public string BranchCode { get; init; } = string.Empty;

    public Guid CoopId { get; init; }

    public string CoopCode { get; init; } = string.Empty;

    public string CoopName { get; init; } = string.Empty;

    public string FarmerName { get; init; } = string.Empty;

    public string? FieldOfficerName { get; init; }

    public DateOnly ChickInDate { get; init; }

    public int AgeDays { get; init; }

    public int Population { get; init; }

    /// <summary>
    /// Cumulative figures up to the last recording.
    /// </summary>
    public PerformanceFigures Performance { get; init; } = PerformanceFigures.Empty;

    public DateOnly? LastRecordingDate { get; init; }

    public bool RecordedToday { get; init; }

    public bool RecordedYesterday { get; init; }

    public DateOnly? LatestStockDate { get; init; }

    public bool StockReportedToday { get; init; }

    public decimal FeedStockKg { get; init; }

    public decimal? AverageDailyFeedKg { get; init; }

    public decimal? FeedDaysLeft { get; init; }

    public IReadOnlyList<DashboardFeedStock> FeedStock { get; init; } = [];

    /// <summary>
    /// Risk flags, see <see cref="DashboardFlags"/>.
    /// </summary>
    public IReadOnlyList<string> Flags { get; init; } = [];

    /// <summary>
    /// The breed standard compared with; null without one.
    /// </summary>
    public string? StandardName { get; init; }

    /// <summary>
    /// The latest figures against the standard (M-77).
    /// </summary>
    public StandardStatus? Standard { get; init; }

    public int AgeOn(DateOnly date) => date.DayNumber - ChickInDate.DayNumber;
}

/// <param name="Quantity">In the base unit.</param>
/// <param name="PackUomCode">Largest unit of the item (e.g. SAK), if any.</param>
public sealed record DashboardFeedStock(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal Quantity,
    string BaseUomCode,
    string? PackUomCode,
    decimal? PackFactor);

/// <summary>
/// Risk flags of a dashboard cycle (M-59).
/// </summary>
public static class DashboardFlags
{
    public const string RecordingLate = "RecordingLate";
    public const string FeedLow = "FeedLow";
    public const string HighDepletion = "HighDepletion";
    public const string NoStockReport = "NoStockReport";
    public const string BelowStandard = "BelowStandard";
}

/// <summary>
/// Cumulative broiler figures of a cycle, as calculated by the server (<c>CyclePerformance</c>) and
/// <see cref="Production.PerformanceCalculator"/>.
/// </summary>
public sealed record PerformanceFigures(
    int InitialPopulation,
    int Mortality,
    int Culling,
    int HarvestedBirds,
    decimal HarvestedWeightKg,
    int Population,
    decimal FeedKg,
    decimal DepletionPercent,
    decimal? AverageWeightKg,
    decimal? LiveWeightKg,
    decimal? Fcr,
    decimal? AgeDays,
    decimal? AdgGram,
    decimal? Ip)
{
    public static readonly PerformanceFigures Empty = new(0, 0, 0, 0, 0, 0, 0, 0, null, null, null, null, null, null);
}

/// <summary>
/// <c>GET cycles/{id}/performance</c>: the daily cumulative performance of a cycle.
/// </summary>
public sealed record CyclePerformanceReport
{
    public Guid CycleId { get; init; }

    public string Number { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateOnly? ChickInDate { get; init; }

    public DateOnly? ClosedDate { get; init; }

    public PerformanceFigures Current { get; init; } = PerformanceFigures.Empty;

    /// <summary>
    /// Frozen figures once the cycle is closed.
    /// </summary>
    public PerformanceFigures? Closing { get; init; }

    public IReadOnlyList<DailyPerformance> Days { get; init; } = [];

    public IReadOnlyList<CycleHarvest> Harvests { get; init; } = [];

    /// <summary>
    /// The breed standard compared with (M-74); null when there is none.
    /// </summary>
    public PerformanceStandard? Standard { get; init; }

    /// <summary>
    /// The server's latest figures against the standard (without queued recordings).
    /// </summary>
    public StandardStatus? StandardStatus { get; init; }
}

/// <summary>
/// The breed standard of a cycle in <c>cycles/{id}/performance</c>: the whole curve and the thresholds, so queued
/// recordings are compared the same way as on the server (M-74, M-76).
/// </summary>
public sealed record PerformanceStandard
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Strain { get; init; }

    /// <summary>
    /// The cycle has no standard of its own and uses the default.
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// Copied when the cycle closed.
    /// </summary>
    public bool IsFrozen { get; init; }

    public StandardThresholds Thresholds { get; init; } = StandardThresholds.Default;

    /// <summary>
    /// Ordered by age.
    /// </summary>
    public IReadOnlyList<StandardRow> Rows { get; init; } = [];
}

public sealed record StandardThresholds(decimal BodyWeightBelowPercent, decimal FcrAbove, decimal DepletionAbovePoints)
{
    public static readonly StandardThresholds Default = new(5m, 0.05m, 1m);
}

public sealed record StandardRow(
    int AgeDays,
    decimal BodyWeightGram,
    decimal? Fcr,
    decimal? DepletionPercent,
    decimal? DailyFeedGram,
    decimal? CumulativeFeedGram);

/// <summary>
/// The latest figures against the standard: body weight and FCR at the last weighing, depletion and feed at the last
/// recording (server <c>StandardStatus</c>, calculated on the device by <see cref="Production.StandardCalculator"/>).
/// </summary>
public sealed record StandardStatus(
    int? WeighingAgeDays,
    decimal? BodyWeightGram,
    decimal? StandardBodyWeightGram,
    decimal? BodyWeightPercent,
    decimal? Fcr,
    decimal? StandardFcr,
    decimal? FcrDeviation,
    int AgeDays,
    decimal DepletionPercent,
    decimal? StandardDepletionPercent,
    decimal? DepletionPoints,
    decimal? CumulativeFeedGram,
    decimal? StandardCumulativeFeedGram,
    decimal? CumulativeFeedPercent,
    bool BodyWeightBelow,
    bool FcrAbove,
    bool DepletionAbove)
{
    public bool BelowStandard => BodyWeightBelow || FcrAbove || DepletionAbove;
}

public sealed record DailyPerformance(
    DateOnly Date,
    int AgeDays,
    int Mortality,
    int Culling,
    decimal FeedKg,
    decimal? AverageBodyWeightGram,
    PerformanceFigures Cumulative);

public sealed record CycleHarvest(Guid Id, DateOnly Date, int AgeDays, int Birds, decimal WeightKg, decimal AverageWeightKg);
