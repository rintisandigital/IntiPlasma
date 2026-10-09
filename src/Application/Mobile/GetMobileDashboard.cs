using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Production;
using Dapper;
using Domain.MasterData.Branches;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Application.Mobile;

/// <summary>
/// Summary for the mobile dashboard (PLAN-MOBILE §6.2, M-55): the running cycles in the caller's scope (PPL = the
/// assigned coops, Manager = the branch) with their cumulative performance, today's recording and stok ayam status,
/// the feed stock of the coop warehouse with an estimate of the days it lasts, and the risk flags (M-59), plus the
/// combined figures of all those cycles (M-60). The app picks what to show per role.
/// </summary>
/// <param name="Date">The reference date, the device's local date (M-56); defaults to the date in Asia/Jakarta.</param>
/// <param name="BranchId">Limits the summary to one accessible branch (M-57); all accessible branches when null.</param>
public sealed record GetMobileDashboardQuery(DateOnly? Date, Guid? BranchId) : IQuery<MobileDashboardResponse>;

public sealed record MobileDashboardResponse(
    DateOnly Date,
    DateTime GeneratedAtUtc,
    DashboardThresholds Thresholds,
    DashboardKpi Kpi,
    IReadOnlyList<DashboardCycleResponse> Cycles);

public sealed record DashboardThresholds(
    int FeedWarningDays,
    int RecordingLateDays,
    decimal DepletionWarningPercent,
    int StockReportFromAgeDays);

/// <param name="DepletionPercent">Deaths and culls of all cycles over their initial population.</param>
/// <param name="Fcr">Feed of all cycles with a body weight over their live plus harvested weight.</param>
/// <param name="Ip">Average IP weighted by the current population.</param>
/// <param name="StockReportExpected">Cycles old enough to report stok ayam daily.</param>
/// <param name="StockReportedToday">Of <paramref name="StockReportExpected"/>, those that reported on the date.</param>
public sealed record DashboardKpi(
    int ActiveCycles,
    int InitialPopulation,
    int Population,
    decimal DepletionPercent,
    decimal? Fcr,
    decimal? Ip,
    int RecordedToday,
    int StockReportExpected,
    int StockReportedToday,
    int CyclesAtRisk,
    decimal FeedStockKg);

/// <param name="AgeDays">Age on the dashboard date.</param>
/// <param name="Population">Current population (initial minus deaths, culls and harvested birds).</param>
/// <param name="Performance">Cumulative figures up to the last recording on or before the date.</param>
/// <param name="FeedStockKg">Feed in the coop warehouse, in kg.</param>
/// <param name="AverageDailyFeedKg">Average feed of the latest recordings with feed usage; null without any.</param>
/// <param name="FeedDaysLeft">Days the feed stock lasts at that average; null when unknown.</param>
/// <param name="Flags">Risk flags, see <see cref="DashboardFlags"/>.</param>
public sealed record DashboardCycleResponse(
    Guid CycleId,
    string Number,
    string Status,
    Guid BranchId,
    string BranchCode,
    Guid CoopId,
    string CoopCode,
    string CoopName,
    string FarmerName,
    string? FieldOfficerName,
    DateOnly ChickInDate,
    int AgeDays,
    int Population,
    CyclePerformance Performance,
    DateOnly? LastRecordingDate,
    bool RecordedToday,
    bool RecordedYesterday,
    DateOnly? LatestStockDate,
    bool StockReportedToday,
    decimal FeedStockKg,
    decimal? AverageDailyFeedKg,
    decimal? FeedDaysLeft,
    IReadOnlyList<DashboardFeedStock> FeedStock,
    IReadOnlyList<string> Flags);

/// <param name="Quantity">In the item's base unit.</param>
/// <param name="PackUomCode">The item's largest unit (e.g. SAK), to show the stock in packs; null without conversions.</param>
/// <param name="PackFactor">Base units per pack.</param>
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
}

public static class MobileDashboardErrors
{
    public static Error FutureDate(DateOnly date) => Error.Problem(
        "MobileDashboard.FutureDate",
        $"The dashboard date {date:yyyy-MM-dd} is in the future");
}

internal sealed class GetMobileDashboardQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IBranchAccess branchAccess,
    IFieldScope fieldScope,
    IDateTimeProvider dateTimeProvider,
    MobileDashboardOptions options)
    : IQueryHandler<GetMobileDashboardQuery, MobileDashboardResponse>
{
    /// <summary>
    /// Asia/Jakarta has no daylight saving time.
    /// </summary>
    private static readonly TimeSpan JakartaOffset = TimeSpan.FromHours(7);

    private static readonly string Sql =
        $"""
         CREATE TEMP TABLE md_cycles ON COMMIT DROP AS
         SELECT pc.id, pc.number, pc.status, pc.branch_id, pc.coop_id, pc.farmer_id, pc.chick_in_date, pc.initial_population,
                pc.initial_population - pc.total_mortality - pc.total_culling - pc.harvested_birds AS population
         FROM partnership.production_cycles pc
         WHERE pc.status IN ('Active', 'Harvesting')
           AND pc.chick_in_date <= @Date
           AND (@AllBranches OR pc.branch_id = ANY(@BranchIds))
           AND (@BranchId::uuid IS NULL OR pc.branch_id = @BranchId)
           AND {FieldScopeSql.CoopId("pc.coop_id")};

         SELECT pc.id AS CycleId, pc.number AS Number, pc.status AS Status, pc.branch_id AS BranchId, b.code AS BranchCode,
                c.id AS CoopId, c.code AS CoopCode, c.name AS CoopName, f.name AS FarmerName,
                NULLIF(trim(concat(u.first_name, ' ', u.last_name)), '') AS FieldOfficerName,
                pc.chick_in_date AS ChickInDate, pc.initial_population AS InitialPopulation, pc.population AS Population,
                (SELECT max(s.date) FROM production.live_bird_stock_entries s
                 WHERE s.cycle_id = pc.id AND s.date <= @Date) AS LatestStockDate
         FROM md_cycles pc
         JOIN master.branches b ON b.id = pc.branch_id
         JOIN master.coops c ON c.id = pc.coop_id
         JOIN master.farmers f ON f.id = pc.farmer_id
         LEFT JOIN identity.users u ON u.id = c.field_officer_user_id
         ORDER BY b.code, c.code;

         SELECT r.cycle_id AS CycleId, r.date AS Date, r.age_days AS AgeDays, r.mortality AS Mortality, r.culling AS Culling,
                COALESCE((SELECT SUM(du.base_quantity) FROM production.daily_recording_usages du
                          JOIN master.items i ON i.id = du.item_id
                          WHERE du.daily_recording_id = r.id AND i.category = 'Feed'), 0) AS FeedKg,
                r.average_body_weight_gram AS AverageBodyWeightGram
         FROM production.daily_recordings r
         JOIN md_cycles pc ON pc.id = r.cycle_id
         WHERE r.date <= @Date
         ORDER BY r.cycle_id, r.date;

         SELECT h.cycle_id AS CycleId, h.date AS Date, h.birds AS Birds, h.weight_kg AS WeightKg
         FROM partnership.cycle_harvests h
         JOIN md_cycles pc ON pc.id = h.cycle_id
         ORDER BY h.date;

         SELECT w.coop_id AS CoopId, i.id AS ItemId, i.code AS ItemCode, i.name AS ItemName, s.quantity AS Quantity,
                bu.code AS BaseUomCode, pack.code AS PackUomCode, pack.factor AS PackFactor
         FROM inventory.stock_balances s
         JOIN master.warehouses w ON w.id = s.warehouse_id
         JOIN master.items i ON i.id = s.item_id
         JOIN master.uoms bu ON bu.id = i.base_uom_id
         LEFT JOIN LATERAL (
             SELECT u.code, cv.factor
             FROM master.item_uom_conversions cv
             JOIN master.uoms u ON u.id = cv.uom_id
             WHERE cv.item_id = i.id
             ORDER BY cv.factor DESC
             LIMIT 1) pack ON TRUE
         WHERE s.quantity <> 0
           AND i.category = 'Feed'
           AND w.coop_id IN (SELECT coop_id FROM md_cycles)
         ORDER BY i.code;
         """;

    public async Task<Result<MobileDashboardResponse>> Handle(GetMobileDashboardQuery query, CancellationToken cancellationToken)
    {
        DateTime utcNow = dateTimeProvider.UtcNow;
        DateOnly date = query.Date ?? DateOnly.FromDateTime(utcNow + JakartaOffset);
        if (date > DateOnly.FromDateTime(utcNow).AddDays(1))
        {
            return Result.Failure<MobileDashboardResponse>(MobileDashboardErrors.FutureDate(date));
        }

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);
        if (query.BranchId is { } branchId && !scope.CanAccess(branchId))
        {
            return Result.Failure<MobileDashboardResponse>(BranchErrors.AccessDenied(branchId));
        }

        FieldScope field = await fieldScope.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            Sql,
            new
            {
                Date = date,
                query.BranchId,
                scope.AllBranches,
                scope.BranchIds,
                FieldRestricted = field.Restricted,
                FieldUserId = field.UserId
            },
            transaction,
            cancellationToken: cancellationToken));

        List<CycleRow> cycles = [.. await multi.ReadAsync<CycleRow>()];
        ILookup<Guid, RecordingRow> recordings = (await multi.ReadAsync<RecordingRow>()).ToLookup(r => r.CycleId);
        ILookup<Guid, HarvestRow> harvests = (await multi.ReadAsync<HarvestRow>()).ToLookup(h => h.CycleId);
        ILookup<Guid, FeedRow> feed = (await multi.ReadAsync<FeedRow>()).ToLookup(f => f.CoopId);

        await transaction.CommitAsync(cancellationToken);

        List<DashboardCycleResponse> results =
        [
            .. cycles.Select(c => MobileDashboardCalculator.BuildCycle(
                c,
                date,
                [.. recordings[c.CycleId].Select(r => new RecordedDay(r.Date, r.AgeDays, r.Mortality, r.Culling, r.FeedKg, r.AverageBodyWeightGram))],
                [.. harvests[c.CycleId].Select(h => new HarvestedBatch(h.Date, h.Birds, h.WeightKg))],
                [.. feed[c.CoopId].Select(f => new DashboardFeedStock(f.ItemId, f.ItemCode, f.ItemName, f.Quantity, f.BaseUomCode, f.PackUomCode, f.PackFactor))],
                options))
        ];

        return new MobileDashboardResponse(
            date,
            utcNow,
            new DashboardThresholds(options.FeedWarningDays, options.RecordingLateDays, options.DepletionWarningPercent, options.StockReportFromAgeDays),
            MobileDashboardCalculator.BuildKpi(results, options),
            results);
    }

    internal sealed record CycleRow
    {
        public Guid CycleId { get; init; }
        public string Number { get; init; }
        public string Status { get; init; }
        public Guid BranchId { get; init; }
        public string BranchCode { get; init; }
        public Guid CoopId { get; init; }
        public string CoopCode { get; init; }
        public string CoopName { get; init; }
        public string FarmerName { get; init; }
        public string? FieldOfficerName { get; init; }
        public DateOnly ChickInDate { get; init; }
        public int InitialPopulation { get; init; }
        public int Population { get; init; }
        public DateOnly? LatestStockDate { get; init; }
    }

    internal sealed record RecordingRow(
        Guid CycleId,
        DateOnly Date,
        int AgeDays,
        int Mortality,
        int Culling,
        decimal FeedKg,
        decimal? AverageBodyWeightGram);

    internal sealed record HarvestRow(Guid CycleId, DateOnly Date, int Birds, decimal WeightKg);

    internal sealed record FeedRow
    {
        public Guid CoopId { get; init; }
        public Guid ItemId { get; init; }
        public string ItemCode { get; init; }
        public string ItemName { get; init; }
        public decimal Quantity { get; init; }
        public string BaseUomCode { get; init; }
        public string? PackUomCode { get; init; }
        public decimal? PackFactor { get; init; }
    }
}

/// <summary>
/// The figures and flags of the mobile dashboard, apart from the database so they can be tested on their own.
/// </summary>
internal static class MobileDashboardCalculator
{
    private const int Decimals = 3;

    public static DashboardCycleResponse BuildCycle(
        GetMobileDashboardQueryHandler.CycleRow cycle,
        DateOnly date,
        IReadOnlyList<RecordedDay> recordings,
        IReadOnlyCollection<HarvestedBatch> harvests,
        IReadOnlyList<DashboardFeedStock> feedStock,
        MobileDashboardOptions options)
    {
        List<DailyPerformance> days = CyclePerformanceBuilder.BuildDays(cycle.InitialPopulation, recordings, harvests);
        CyclePerformance performance = CyclePerformanceBuilder.Current(cycle.InitialPopulation, days);

        DateOnly? lastRecording = recordings.Count > 0 ? recordings.Max(r => r.Date) : null;
        int ageDays = date.DayNumber - cycle.ChickInDate.DayNumber;
        decimal feedStockKg = feedStock.Sum(f => f.Quantity);

        var withFeed = recordings.Where(r => r.FeedKg > 0).OrderByDescending(r => r.Date).Take(options.FeedAverageDays).ToList();
        decimal? averageFeed = withFeed.Count > 0 ? Round(withFeed.Average(r => r.FeedKg)) : null;
        decimal? daysLeft = averageFeed is > 0 ? decimal.Round(feedStockKg / averageFeed.Value, 1, MidpointRounding.AwayFromZero) : null;

        bool stockReportedToday = cycle.LatestStockDate == date;

        var flags = new List<string>();
        if (date.DayNumber - (lastRecording ?? cycle.ChickInDate).DayNumber > options.RecordingLateDays)
        {
            flags.Add(DashboardFlags.RecordingLate);
        }

        if (daysLeft < options.FeedWarningDays)
        {
            flags.Add(DashboardFlags.FeedLow);
        }

        if (performance.DepletionPercent >= options.DepletionWarningPercent)
        {
            flags.Add(DashboardFlags.HighDepletion);
        }

        if (ageDays >= options.StockReportFromAgeDays && !stockReportedToday)
        {
            flags.Add(DashboardFlags.NoStockReport);
        }

        return new DashboardCycleResponse(
            cycle.CycleId,
            cycle.Number,
            cycle.Status,
            cycle.BranchId,
            cycle.BranchCode,
            cycle.CoopId,
            cycle.CoopCode,
            cycle.CoopName,
            cycle.FarmerName,
            cycle.FieldOfficerName,
            cycle.ChickInDate,
            ageDays,
            cycle.Population,
            performance,
            lastRecording,
            lastRecording == date,
            recordings.Any(r => r.Date == date.AddDays(-1)),
            cycle.LatestStockDate,
            stockReportedToday,
            feedStockKg,
            averageFeed,
            daysLeft,
            feedStock,
            flags);
    }

    public static DashboardKpi BuildKpi(IReadOnlyList<DashboardCycleResponse> cycles, MobileDashboardOptions options)
    {
        int initial = cycles.Sum(c => c.Performance.InitialPopulation);
        int depleted = cycles.Sum(c => c.Performance.Mortality + c.Performance.Culling);

        var weighed = cycles.Where(c => c.Performance.LiveWeightKg is > 0).ToList();
        decimal liveWeight = weighed.Sum(c => c.Performance.LiveWeightKg!.Value);
        decimal? fcr = liveWeight > 0 ? Round(weighed.Sum(c => c.Performance.FeedKg) / liveWeight) : null;

        var withIp = cycles.Where(c => c.Performance.Ip is not null).ToList();
        int ipPopulation = withIp.Sum(c => c.Population);
        decimal? ip = ipPopulation > 0 ? Round(withIp.Sum(c => c.Performance.Ip!.Value * c.Population) / ipPopulation) : null;

        var expected = cycles.Where(c => c.AgeDays >= options.StockReportFromAgeDays).ToList();

        return new DashboardKpi(
            cycles.Count,
            initial,
            cycles.Sum(c => c.Population),
            initial == 0 ? 0 : Round(depleted * 100m / initial),
            fcr,
            ip,
            cycles.Count(c => c.RecordedToday),
            expected.Count,
            expected.Count(c => c.StockReportedToday),
            cycles.Count(c => c.Flags.Count > 0),
            cycles.Sum(c => c.FeedStockKg));
    }

    private static decimal Round(decimal value) => decimal.Round(value, Decimals, MidpointRounding.AwayFromZero);
}
