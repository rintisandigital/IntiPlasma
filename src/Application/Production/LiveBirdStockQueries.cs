using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.MasterData.Branches;
using SharedKernel;

namespace Application.Production;

/// <summary>
/// Entries of stok ayam harian: of one cycle, or of the branches the caller may see on a date / in a period.
/// </summary>
public sealed record GetLiveBirdStockEntriesQuery(
    Guid? CycleId = null,
    Guid? BranchId = null,
    DateOnly? Date = null,
    DateOnly? From = null,
    DateOnly? To = null) : IQuery<IReadOnlyList<LiveBirdStockEntryResponse>>;

/// <summary>
/// Rekap stok ayam (PLAN-MOBILE M-28, M-51): per running cycle the entries of its latest report on or before
/// <see cref="Date"/>, totalled per weight range.
/// </summary>
public sealed record GetLiveBirdStockSummaryQuery(DateOnly Date, Guid? BranchId = null) : IQuery<LiveBirdStockSummaryResponse>;

public sealed record LiveBirdStockEntryResponse
{
    public Guid Id { get; init; }
    public Guid CycleId { get; init; }
    public string CycleNumber { get; init; }
    public Guid BranchId { get; init; }
    public Guid CoopId { get; init; }
    public string CoopCode { get; init; }
    public string CoopName { get; init; }
    public DateOnly Date { get; init; }
    public int AgeDays { get; init; }
    public Guid WeightRangeId { get; init; }
    public string WeightRangeCode { get; init; }
    public string WeightRangeName { get; init; }
    public int Birds { get; init; }
    public decimal WeightKg { get; init; }
    public decimal AverageWeightKg { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ModifiedAtUtc { get; init; }
}

/// <param name="Ranges">Totals per weight range: the active ranges plus inactive ones still reported.</param>
/// <param name="Coops">Every running cycle, reported or not.</param>
public sealed record LiveBirdStockSummaryResponse(
    DateOnly Date,
    Guid? BranchId,
    IReadOnlyList<LiveBirdStockRangeTotal> Ranges,
    IReadOnlyList<LiveBirdStockCoopSummary> Coops)
{
    public int TotalBirds => Ranges.Sum(r => r.Birds);

    public decimal TotalWeightKg => Ranges.Sum(r => r.WeightKg);

    public int ReportedCoops => Coops.Count(c => c.ReportDate is not null);
}

public sealed record LiveBirdStockRangeTotal(
    Guid WeightRangeId,
    string Code,
    string Name,
    decimal? MinWeightKg,
    decimal? MaxWeightKg,
    int Birds,
    decimal WeightKg,
    int Coops)
{
    public decimal AverageWeightKg => Birds == 0 ? 0 : Math.Round(WeightKg / Birds, 3);
}

/// <param name="ReportDate">Date of the entries used; null when the cycle has not reported yet.</param>
/// <param name="DataAgeDays">Days between <see cref="ReportDate"/> and the summary date.</param>
/// <param name="Population">Current population of the cycle.</param>
public sealed record LiveBirdStockCoopSummary(
    Guid CycleId,
    string CycleNumber,
    Guid BranchId,
    string BranchCode,
    Guid CoopId,
    string CoopCode,
    string CoopName,
    string FarmerName,
    string? FieldOfficerName,
    int AgeDays,
    int Population,
    DateOnly? ReportDate,
    int? DataAgeDays,
    IReadOnlyList<LiveBirdStockCoopRange> Entries)
{
    /// <summary>
    /// Older than one day (M-51).
    /// </summary>
    public bool IsStale => DataAgeDays > 1;

    public int TotalBirds => Entries.Sum(e => e.Birds);

    public decimal TotalWeightKg => Entries.Sum(e => e.WeightKg);
}

public sealed record LiveBirdStockCoopRange(Guid WeightRangeId, string Code, int Birds, decimal WeightKg)
{
    public decimal AverageWeightKg => Birds == 0 ? 0 : Math.Round(WeightKg / Birds, 3);
}

internal static class LiveBirdStockSql
{
    public const string Select =
        """
        SELECT e.id AS Id, e.cycle_id AS CycleId, pc.number AS CycleNumber, e.branch_id AS BranchId, e.coop_id AS CoopId,
               c.code AS CoopCode, c.name AS CoopName, e.date AS Date, e.age_days AS AgeDays,
               e.weight_range_id AS WeightRangeId, r.code AS WeightRangeCode, r.name AS WeightRangeName,
               e.birds AS Birds, e.weight_kg AS WeightKg, round(e.weight_kg / e.birds, 3) AS AverageWeightKg,
               e.notes AS Notes, e.created_at_utc AS CreatedAtUtc, e.modified_at_utc AS ModifiedAtUtc
        FROM production.live_bird_stock_entries e
        JOIN partnership.production_cycles pc ON pc.id = e.cycle_id
        JOIN master.coops c ON c.id = e.coop_id
        JOIN master.weight_ranges r ON r.id = e.weight_range_id
        """;
}

internal sealed class GetLiveBirdStockEntriesQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IBranchAccess branchAccess,
    IFieldScope fieldScope) : IQueryHandler<GetLiveBirdStockEntriesQuery, IReadOnlyList<LiveBirdStockEntryResponse>>
{
    public async Task<Result<IReadOnlyList<LiveBirdStockEntryResponse>>> Handle(
        GetLiveBirdStockEntriesQuery query,
        CancellationToken cancellationToken)
    {
        if (query.CycleId is null && query.Date is null && query.From is null)
        {
            return Result.Failure<IReadOnlyList<LiveBirdStockEntryResponse>>(Error.Problem(
                "LiveBirdStock.FilterRequired", "Filter by cycle, date or period"));
        }

        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);
        if (query.BranchId is { } branchId && !scope.CanAccess(branchId))
        {
            return Result.Failure<IReadOnlyList<LiveBirdStockEntryResponse>>(BranchErrors.AccessDenied(branchId));
        }

        FieldScope field = await fieldScope.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        IEnumerable<LiveBirdStockEntryResponse> entries = await connection.QueryAsync<LiveBirdStockEntryResponse>(new CommandDefinition(
            $"""
             {LiveBirdStockSql.Select}
             WHERE (@AllBranches OR e.branch_id = ANY(@BranchIds))
               AND {FieldScopeSql.CoopId("e.coop_id")}
               AND (@CycleId::uuid IS NULL OR e.cycle_id = @CycleId)
               AND (@BranchId::uuid IS NULL OR e.branch_id = @BranchId)
               AND (@Date::date IS NULL OR e.date = @Date)
               AND (@From::date IS NULL OR e.date >= @From)
               AND (@To::date IS NULL OR e.date <= @To)
             ORDER BY e.date DESC, c.code, r.sort_order, r.code
             """,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                FieldRestricted = field.Restricted,
                FieldUserId = field.UserId,
                query.CycleId,
                query.BranchId,
                query.Date,
                query.From,
                query.To
            },
            cancellationToken: cancellationToken));

        return entries.ToList();
    }
}

internal sealed class GetLiveBirdStockSummaryQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IBranchAccess branchAccess,
    IFieldScope fieldScope) : IQueryHandler<GetLiveBirdStockSummaryQuery, LiveBirdStockSummaryResponse>
{
    private static readonly string Sql =
        $"""
         CREATE TEMP TABLE lbs_cycles ON COMMIT DROP AS
         SELECT pc.id, pc.number, pc.branch_id, pc.coop_id, pc.farmer_id, pc.chick_in_date,
                pc.initial_population - pc.total_mortality - pc.total_culling - pc.harvested_birds AS population,
                (SELECT max(e.date) FROM production.live_bird_stock_entries e
                 WHERE e.cycle_id = pc.id AND e.date <= @Date) AS report_date
         FROM partnership.production_cycles pc
         WHERE pc.status IN ('Active', 'Harvesting')
           AND pc.chick_in_date <= @Date
           AND (@AllBranches OR pc.branch_id = ANY(@BranchIds))
           AND (@BranchId::uuid IS NULL OR pc.branch_id = @BranchId)
           AND {FieldScopeSql.CoopId("pc.coop_id")};

         SELECT pc.id AS CycleId, pc.number AS CycleNumber, pc.branch_id AS BranchId, b.code AS BranchCode,
                c.id AS CoopId, c.code AS CoopCode, c.name AS CoopName, f.name AS FarmerName,
                NULLIF(trim(concat(u.first_name, ' ', u.last_name)), '') AS FieldOfficerName,
                @Date - pc.chick_in_date AS AgeDays, pc.population AS Population, pc.report_date AS ReportDate
         FROM lbs_cycles pc
         JOIN master.branches b ON b.id = pc.branch_id
         JOIN master.coops c ON c.id = pc.coop_id
         JOIN master.farmers f ON f.id = pc.farmer_id
         LEFT JOIN identity.users u ON u.id = c.field_officer_user_id
         ORDER BY b.code, c.code;

         SELECT e.cycle_id AS CycleId, e.weight_range_id AS WeightRangeId, r.code AS Code, e.birds AS Birds, e.weight_kg AS WeightKg
         FROM production.live_bird_stock_entries e
         JOIN lbs_cycles pc ON pc.id = e.cycle_id AND e.date = pc.report_date
         JOIN master.weight_ranges r ON r.id = e.weight_range_id
         ORDER BY r.sort_order, r.code;

         SELECT r.id AS WeightRangeId, r.code AS Code, r.name AS Name, r.min_weight_kg AS MinWeightKg,
                r.max_weight_kg AS MaxWeightKg
         FROM master.weight_ranges r
         WHERE r.is_active
            OR EXISTS (SELECT 1 FROM production.live_bird_stock_entries e
                       JOIN lbs_cycles pc ON pc.id = e.cycle_id AND e.date = pc.report_date
                       WHERE e.weight_range_id = r.id)
         ORDER BY r.sort_order, r.code;
         """;

    public async Task<Result<LiveBirdStockSummaryResponse>> Handle(GetLiveBirdStockSummaryQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);
        if (query.BranchId is { } branchId && !scope.CanAccess(branchId))
        {
            return Result.Failure<LiveBirdStockSummaryResponse>(BranchErrors.AccessDenied(branchId));
        }

        FieldScope field = await fieldScope.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            Sql,
            new
            {
                query.Date,
                query.BranchId,
                scope.AllBranches,
                scope.BranchIds,
                FieldRestricted = field.Restricted,
                FieldUserId = field.UserId
            },
            transaction,
            cancellationToken: cancellationToken));

        List<CycleRow> cycles = [.. await multi.ReadAsync<CycleRow>()];
        ILookup<Guid, EntryRow> entries = (await multi.ReadAsync<EntryRow>()).ToLookup(e => e.CycleId);
        List<RangeRow> ranges = [.. await multi.ReadAsync<RangeRow>()];

        await transaction.CommitAsync(cancellationToken);

        List<LiveBirdStockCoopSummary> coops =
        [
            .. cycles.Select(c => new LiveBirdStockCoopSummary(
                c.CycleId,
                c.CycleNumber,
                c.BranchId,
                c.BranchCode,
                c.CoopId,
                c.CoopCode,
                c.CoopName,
                c.FarmerName,
                c.FieldOfficerName,
                c.AgeDays,
                c.Population,
                c.ReportDate,
                c.ReportDate is { } reported ? query.Date.DayNumber - reported.DayNumber : null,
                [.. entries[c.CycleId].Select(e => new LiveBirdStockCoopRange(e.WeightRangeId, e.Code, e.Birds, e.WeightKg))]))
        ];

        List<LiveBirdStockRangeTotal> totals =
        [
            .. ranges.Select(r =>
            {
                var used = coops.SelectMany(c => c.Entries).Where(e => e.WeightRangeId == r.WeightRangeId).ToList();

                return new LiveBirdStockRangeTotal(
                    r.WeightRangeId,
                    r.Code,
                    r.Name,
                    r.MinWeightKg,
                    r.MaxWeightKg,
                    used.Sum(e => e.Birds),
                    used.Sum(e => e.WeightKg),
                    used.Count);
            })
        ];

        return new LiveBirdStockSummaryResponse(query.Date, query.BranchId, totals, coops);
    }

    internal sealed record CycleRow
    {
        public Guid CycleId { get; init; }
        public string CycleNumber { get; init; }
        public Guid BranchId { get; init; }
        public string BranchCode { get; init; }
        public Guid CoopId { get; init; }
        public string CoopCode { get; init; }
        public string CoopName { get; init; }
        public string FarmerName { get; init; }
        public string? FieldOfficerName { get; init; }
        public int AgeDays { get; init; }
        public int Population { get; init; }
        public DateOnly? ReportDate { get; init; }
    }

    internal sealed record EntryRow(Guid CycleId, Guid WeightRangeId, string Code, int Birds, decimal WeightKg);

    internal sealed record RangeRow(Guid WeightRangeId, string Code, string Name, decimal? MinWeightKg, decimal? MaxWeightKg);
}
