using System.Data.Common;
using System.Text.Json;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.Partnership.Cycles;
using Domain.Production.DailyRecordings;
using SharedKernel;

namespace Application.Production;

public sealed record GetDailyRecordingsQuery(Guid CycleId, DateOnly? From, DateOnly? To) : IQuery<IReadOnlyList<DailyRecordingResponse>>;

public sealed record GetDailyRecordingByIdQuery(Guid DailyRecordingId) : IQuery<DailyRecordingResponse>;

/// <summary>
/// Performa siklus per hari (populasi, deplesi, pakan kumulatif, BW, FCR, ADG, IP), the harvests, the current
/// figures and, once closed, the frozen closing performance.
/// </summary>
public sealed record GetCyclePerformanceQuery(Guid CycleId) : IQuery<CyclePerformanceResponse>;

public sealed record DailyRecordingResponse
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

    /// <summary>
    /// Detail endpoint only.
    /// </summary>
    public IReadOnlyList<UsageResponse>? Usages { get; init; }

    /// <summary>
    /// Detail endpoint only: revision history, newest first, with the values before each revision.
    /// </summary>
    public IReadOnlyList<RevisionResponse>? Revisions { get; init; }
}

public sealed record UsageResponse(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string ItemCategory,
    string UomCode,
    decimal Quantity,
    decimal BaseQuantity,
    string BaseUomCode,
    decimal Value);

public sealed record RevisionResponse(int RevisionNumber, string Reason, JsonElement PreviousValues, Guid? RevisedBy, DateTime RevisedAtUtc);

public sealed record CyclePerformanceResponse(
    Guid CycleId,
    string Number,
    string Status,
    DateOnly? ChickInDate,
    DateOnly? ClosedDate,
    CyclePerformance Current,
    CyclePerformance? Closing,
    IReadOnlyList<DailyPerformance> Days,
    IReadOnlyList<HarvestResponse> Harvests);

public sealed record DailyPerformance(
    DateOnly Date,
    int AgeDays,
    int Mortality,
    int Culling,
    decimal FeedKg,
    decimal? AverageBodyWeightGram,
    CyclePerformance Cumulative);

public sealed record HarvestResponse(Guid Id, DateOnly Date, int AgeDays, int Birds, decimal WeightKg, decimal AverageWeightKg, string? Notes);

internal static class DailyRecordingSql
{
    public const string Select =
        """
        SELECT r.id AS Id, r.cycle_id AS CycleId, r.branch_id AS BranchId, r.date AS Date, r.age_days AS AgeDays,
               r.mortality AS Mortality, r.culling AS Culling, r.average_body_weight_gram AS AverageBodyWeightGram,
               COALESCE((SELECT SUM(u.base_quantity) FROM production.daily_recording_usages u
                         JOIN master.items i ON i.id = u.item_id
                         WHERE u.daily_recording_id = r.id AND i.category = 'Feed'), 0) AS FeedKg,
               r.notes AS Notes, r.revision_number AS RevisionNumber, r.created_at_utc AS CreatedAtUtc
        FROM production.daily_recordings r
        """;
}

internal sealed class GetDailyRecordingsQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetDailyRecordingsQuery, IReadOnlyList<DailyRecordingResponse>>
{
    public async Task<Result<IReadOnlyList<DailyRecordingResponse>>> Handle(GetDailyRecordingsQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        Result access = await ProductionReadSupport.EnsureCycleAccessAsync(connection, branchAccess, query.CycleId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<IReadOnlyList<DailyRecordingResponse>>(access.Error);
        }

        IEnumerable<DailyRecordingResponse> recordings = await connection.QueryAsync<DailyRecordingResponse>(new CommandDefinition(
            $"""
            {DailyRecordingSql.Select}
            WHERE r.cycle_id = @CycleId
              AND (@From::date IS NULL OR r.date >= @From)
              AND (@To::date IS NULL OR r.date <= @To)
            ORDER BY r.date
            """,
            new { query.CycleId, query.From, query.To },
            cancellationToken: cancellationToken));

        return recordings.ToList();
    }
}

internal sealed class GetDailyRecordingByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetDailyRecordingByIdQuery, DailyRecordingResponse>
{
    public async Task<Result<DailyRecordingResponse>> Handle(GetDailyRecordingByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {DailyRecordingSql.Select}
            WHERE r.id = @Id;

            SELECT u.item_id AS ItemId, i.code AS ItemCode, i.name AS ItemName, i.category AS ItemCategory, uo.code AS UomCode,
                   u.quantity AS Quantity, u.base_quantity AS BaseQuantity, bu.code AS BaseUomCode, u.value AS Value
            FROM production.daily_recording_usages u
            JOIN master.items i ON i.id = u.item_id
            JOIN master.uoms uo ON uo.id = u.uom_id
            JOIN master.uoms bu ON bu.id = i.base_uom_id
            WHERE u.daily_recording_id = @Id
            ORDER BY i.category, i.code;

            SELECT v.revision_number AS RevisionNumber, v.reason AS Reason, v.previous_values::text AS PreviousValues,
                   v.revised_by AS RevisedBy, v.revised_at_utc AS RevisedAtUtc
            FROM production.daily_recording_revisions v
            WHERE v.daily_recording_id = @Id
            ORDER BY v.revision_number DESC;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { Id = query.DailyRecordingId }, cancellationToken: cancellationToken));

        DailyRecordingResponse? recording = await multi.ReadSingleOrDefaultAsync<DailyRecordingResponse>();
        if (recording is null)
        {
            return Result.Failure<DailyRecordingResponse>(DailyRecordingErrors.NotFound(query.DailyRecordingId));
        }

        Result access = await branchAccess.EnsureAccessAsync(recording.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<DailyRecordingResponse>(access.Error);
        }

        List<UsageResponse> usages = [.. await multi.ReadAsync<UsageResponse>()];
        List<RevisionResponse> revisions =
        [
            .. (await multi.ReadAsync<RevisionRow>()).Select(r => new RevisionResponse(
                r.RevisionNumber, r.Reason, JsonSerializer.Deserialize<JsonElement>(r.PreviousValues), r.RevisedBy, r.RevisedAtUtc))
        ];

        return recording with { Usages = usages, Revisions = revisions };
    }

    private sealed record RevisionRow(int RevisionNumber, string Reason, string PreviousValues, Guid? RevisedBy, DateTime RevisedAtUtc);
}

internal sealed class GetCyclePerformanceQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetCyclePerformanceQuery, CyclePerformanceResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<CyclePerformanceResponse>> Handle(GetCyclePerformanceQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT c.id AS Id, c.number AS Number, c.status AS Status, c.branch_id AS BranchId, c.chick_in_date AS ChickInDate,
                   c.initial_population AS InitialPopulation, c.closed_date AS ClosedDate,
                   c.closing_performance::text AS ClosingPerformance
            FROM partnership.production_cycles c
            WHERE c.id = @CycleId;

            SELECT r.date AS Date, r.age_days AS AgeDays, r.mortality AS Mortality, r.culling AS Culling,
                   r.average_body_weight_gram AS AverageBodyWeightGram,
                   COALESCE((SELECT SUM(u.base_quantity) FROM production.daily_recording_usages u
                             JOIN master.items i ON i.id = u.item_id
                             WHERE u.daily_recording_id = r.id AND i.category = 'Feed'), 0) AS FeedKg
            FROM production.daily_recordings r
            WHERE r.cycle_id = @CycleId
            ORDER BY r.date;

            SELECT h.id AS Id, h.date AS Date, h.age_days AS AgeDays, h.birds AS Birds, h.weight_kg AS WeightKg,
                   round(h.weight_kg / h.birds, 3) AS AverageWeightKg, h.notes AS Notes
            FROM partnership.cycle_harvests h
            WHERE h.cycle_id = @CycleId
            ORDER BY h.date;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.CycleId }, cancellationToken: cancellationToken));

        CycleRow? cycle = await multi.ReadSingleOrDefaultAsync<CycleRow>();
        if (cycle is null)
        {
            return Result.Failure<CyclePerformanceResponse>(CycleErrors.NotFound(query.CycleId));
        }

        Result access = await branchAccess.EnsureAccessAsync(cycle.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CyclePerformanceResponse>(access.Error);
        }

        List<DayRow> recordings = [.. await multi.ReadAsync<DayRow>()];
        List<HarvestResponse> harvests = [.. await multi.ReadAsync<HarvestResponse>()];

        List<DailyPerformance> days = BuildDays(cycle.InitialPopulation ?? 0, recordings, harvests);

        CyclePerformance current = days.Count > 0
            ? days[^1].Cumulative
            : CyclePerformance.Calculate(cycle.InitialPopulation ?? 0, 0, 0, 0, 0, 0, null, null);

        CyclePerformance? closing = cycle.ClosingPerformance is null
            ? null
            : JsonSerializer.Deserialize<CyclePerformance>(cycle.ClosingPerformance, JsonOptions);

        return new CyclePerformanceResponse(
            cycle.Id, cycle.Number, cycle.Status, cycle.ChickInDate, cycle.ClosedDate, current, closing, days, harvests);
    }

    /// <summary>
    /// Accumulates the recordings day by day; the body weight carries forward from the last weighing.
    /// </summary>
    private static List<DailyPerformance> BuildDays(int initialPopulation, List<DayRow> recordings, List<HarvestResponse> harvests)
    {
        var days = new List<DailyPerformance>();
        int mortality = 0;
        int culling = 0;
        decimal feedKg = 0;
        decimal? lastBodyWeightGram = null;

        foreach (DayRow day in recordings)
        {
            mortality += day.Mortality;
            culling += day.Culling;
            feedKg += day.FeedKg;
            lastBodyWeightGram = day.AverageBodyWeightGram ?? lastBodyWeightGram;

            var harvestedToDate = harvests.Where(h => h.Date <= day.Date).ToList();

            var cumulative = CyclePerformance.Calculate(
                initialPopulation,
                mortality,
                culling,
                harvestedToDate.Sum(h => h.Birds),
                harvestedToDate.Sum(h => h.WeightKg),
                feedKg,
                lastBodyWeightGram / 1000m,
                day.AgeDays);

            days.Add(new DailyPerformance(
                day.Date, day.AgeDays, day.Mortality, day.Culling, day.FeedKg, day.AverageBodyWeightGram, cumulative));
        }

        return days;
    }

    internal sealed class CycleRow
    {
        public Guid Id { get; set; }
        public string Number { get; set; }
        public string Status { get; set; }
        public Guid BranchId { get; set; }
        public DateOnly? ChickInDate { get; set; }
        public int? InitialPopulation { get; set; }
        public DateOnly? ClosedDate { get; set; }
        public string? ClosingPerformance { get; set; }
    }

    internal sealed class DayRow
    {
        public DateOnly Date { get; set; }
        public int AgeDays { get; set; }
        public int Mortality { get; set; }
        public int Culling { get; set; }
        public decimal? AverageBodyWeightGram { get; set; }
        public decimal FeedKg { get; set; }
    }
}

internal static class ProductionReadSupport
{
    public static async Task<Result> EnsureCycleAccessAsync(
        DbConnection connection,
        IBranchAccess branchAccess,
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        Guid? branchId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT c.branch_id FROM partnership.production_cycles c WHERE c.id = @CycleId",
            new { CycleId = cycleId },
            cancellationToken: cancellationToken));

        if (branchId is null)
        {
            return Result.Failure(CycleErrors.NotFound(cycleId));
        }

        return await branchAccess.EnsureAccessAsync(branchId.Value, cancellationToken);
    }
}
