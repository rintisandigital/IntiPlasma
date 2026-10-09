using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using SharedKernel;

namespace Application.Mobile;

/// <summary>
/// Data the mobile app needs to fill in daily forms offline (PLAN-MOBILE §3.6, M-40): the running cycles in the
/// caller's scope with their population and recent recording dates, the feed/OVK items with their units, and the
/// quantity in stock of the coop warehouses (values and costs are left out on purpose), and for stok ayam harian
/// (M-52) the active weight ranges and each cycle's latest report.
/// </summary>
public sealed record GetFieldContextQuery : IQuery<FieldContextResponse>;

/// <param name="ServerDate">The server's date; recordings may be at most one day after it.</param>
public sealed record FieldContextResponse(
    DateOnly ServerDate,
    DateTime GeneratedAtUtc,
    IReadOnlyList<FieldCycleResponse> Cycles,
    IReadOnlyList<FieldItemResponse> Items,
    IReadOnlyList<FieldStockResponse> Stock,
    IReadOnlyList<FieldWeightRangeResponse> WeightRanges);

public sealed record FieldCycleResponse
{
    public Guid Id { get; init; }

    public string Number { get; init; }

    public string Status { get; init; }

    public Guid BranchId { get; init; }

    public Guid CoopId { get; init; }

    public string CoopCode { get; init; }

    public string CoopName { get; init; }

    public Guid FarmerId { get; init; }

    public string FarmerName { get; init; }

    /// <summary>
    /// Gudang kandang; null when the coop has none yet (usage cannot be recorded then).
    /// </summary>
    public Guid? WarehouseId { get; init; }

    public DateOnly ChickInDate { get; init; }

    public int InitialPopulation { get; init; }

    /// <summary>
    /// Initial population minus mortality, culling and harvested birds.
    /// </summary>
    public int CurrentPopulation { get; init; }

    public DateOnly? LastRecordingDate { get; init; }

    /// <summary>
    /// Dates already recorded within <see cref="GetFieldContextQueryHandler.RecentDays"/> days of the server date.
    /// </summary>
    public IReadOnlyList<DateOnly> RecordedDates { get; init; } = [];

    /// <summary>
    /// Date of the latest stok ayam report (M-52); null when the cycle has none.
    /// </summary>
    public DateOnly? LatestStockDate { get; init; }

    /// <summary>
    /// The entries of <see cref="LatestStockDate"/>: today's list, or the base for "Salin dari kemarin".
    /// </summary>
    public IReadOnlyList<FieldStockEntryResponse> LatestStock { get; init; } = [];
}

/// <summary>
/// Active weight range for stok ayam harian, in display order.
/// </summary>
public sealed record FieldWeightRangeResponse(Guid Id, string Code, string Name, decimal? MinWeightKg, decimal? MaxWeightKg);

public sealed record FieldStockEntryResponse(Guid Id, Guid WeightRangeId, int Birds, decimal WeightKg, string? Notes);

/// <param name="Category"><c>Feed</c> or <c>Ovk</c>.</param>
/// <param name="Uoms">Units the item can be recorded in, the base unit first (factor 1).</param>
public sealed record FieldItemResponse(
    Guid Id,
    string Code,
    string Name,
    string Category,
    Guid BaseUomId,
    string BaseUomCode,
    IReadOnlyList<FieldUomResponse> Uoms);

/// <param name="Factor">Base units per one of this unit.</param>
public sealed record FieldUomResponse(Guid UomId, string Code, decimal Factor);

/// <param name="Quantity">In the item's base unit.</param>
public sealed record FieldStockResponse(Guid WarehouseId, Guid ItemId, decimal Quantity);

internal sealed class GetFieldContextQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IBranchAccess branchAccess,
    IFieldScope fieldScope,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetFieldContextQuery, FieldContextResponse>
{
    public const int RecentDays = 14;

    private static readonly string Sql =
        $"""
         CREATE TEMP TABLE fc_cycles ON COMMIT DROP AS
         SELECT pc.id, pc.number, pc.status, pc.branch_id, pc.coop_id, pc.farmer_id, pc.chick_in_date,
                pc.initial_population, pc.total_mortality, pc.total_culling, pc.harvested_birds
         FROM partnership.production_cycles pc
         WHERE pc.status IN ('Active', 'Harvesting')
           AND (@AllBranches OR pc.branch_id = ANY(@BranchIds))
           AND {FieldScopeSql.CoopId("pc.coop_id")};

         SELECT pc.id AS Id, pc.number AS Number, pc.status AS Status, pc.branch_id AS BranchId,
                c.id AS CoopId, c.code AS CoopCode, c.name AS CoopName, f.id AS FarmerId, f.name AS FarmerName,
                w.id AS WarehouseId, pc.chick_in_date AS ChickInDate, pc.initial_population AS InitialPopulation,
                pc.initial_population - pc.total_mortality - pc.total_culling - pc.harvested_birds AS CurrentPopulation,
                (SELECT MAX(r.date) FROM production.daily_recordings r WHERE r.cycle_id = pc.id) AS LastRecordingDate,
                (SELECT MAX(s.date) FROM production.live_bird_stock_entries s WHERE s.cycle_id = pc.id) AS LatestStockDate
         FROM fc_cycles pc
         JOIN master.coops c ON c.id = pc.coop_id
         JOIN master.farmers f ON f.id = pc.farmer_id
         LEFT JOIN master.warehouses w ON w.coop_id = pc.coop_id
         ORDER BY c.code;

         SELECT r.cycle_id AS CycleId, r.date AS Date
         FROM production.daily_recordings r
         JOIN fc_cycles pc ON pc.id = r.cycle_id
         WHERE r.date >= @Since
         ORDER BY r.date;

         SELECT i.id AS Id, i.code AS Code, i.name AS Name, i.category AS Category, i.base_uom_id AS BaseUomId,
                u.code AS BaseUomCode
         FROM master.items i
         JOIN master.uoms u ON u.id = i.base_uom_id
         WHERE i.is_active AND i.category IN ('Feed', 'Ovk')
         ORDER BY i.category, i.code;

         SELECT cv.item_id AS ItemId, cv.uom_id AS UomId, u.code AS Code, cv.factor AS Factor
         FROM master.item_uom_conversions cv
         JOIN master.items i ON i.id = cv.item_id
         JOIN master.uoms u ON u.id = cv.uom_id
         WHERE i.is_active AND i.category IN ('Feed', 'Ovk')
         ORDER BY cv.factor;

         SELECT s.warehouse_id AS WarehouseId, s.item_id AS ItemId, s.quantity AS Quantity
         FROM inventory.stock_balances s
         JOIN master.warehouses w ON w.id = s.warehouse_id
         JOIN master.items i ON i.id = s.item_id
         WHERE s.quantity <> 0
           AND i.category IN ('Feed', 'Ovk')
           AND w.coop_id IN (SELECT coop_id FROM fc_cycles);

         SELECT s.cycle_id AS CycleId, s.id AS Id, s.weight_range_id AS WeightRangeId, s.birds AS Birds,
                s.weight_kg AS WeightKg, s.notes AS Notes
         FROM production.live_bird_stock_entries s
         JOIN fc_cycles pc ON pc.id = s.cycle_id
         WHERE s.date = (SELECT MAX(l.date) FROM production.live_bird_stock_entries l WHERE l.cycle_id = s.cycle_id);

         SELECT r.id AS Id, r.code AS Code, r.name AS Name, r.min_weight_kg AS MinWeightKg, r.max_weight_kg AS MaxWeightKg
         FROM master.weight_ranges r
         WHERE r.is_active
         ORDER BY r.sort_order, r.code;
         """;

    public async Task<Result<FieldContextResponse>> Handle(GetFieldContextQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);
        FieldScope field = await fieldScope.GetScopeAsync(cancellationToken);
        DateTime utcNow = dateTimeProvider.UtcNow;
        var serverDate = DateOnly.FromDateTime(utcNow);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            Sql,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                FieldRestricted = field.Restricted,
                FieldUserId = field.UserId,
                Since = serverDate.AddDays(-RecentDays)
            },
            transaction,
            cancellationToken: cancellationToken));

        List<FieldCycleResponse> cycles = [.. await multi.ReadAsync<FieldCycleResponse>()];
        ILookup<Guid, DateOnly> recorded = (await multi.ReadAsync<RecordedDateRow>()).ToLookup(r => r.CycleId, r => r.Date);
        List<ItemRow> items = [.. await multi.ReadAsync<ItemRow>()];
        ILookup<Guid, ConversionRow> conversions = (await multi.ReadAsync<ConversionRow>()).ToLookup(c => c.ItemId);
        List<FieldStockResponse> stock = [.. await multi.ReadAsync<FieldStockResponse>()];
        ILookup<Guid, StockEntryRow> latestStock = (await multi.ReadAsync<StockEntryRow>()).ToLookup(e => e.CycleId);
        List<FieldWeightRangeResponse> weightRanges = [.. await multi.ReadAsync<FieldWeightRangeResponse>()];

        await transaction.CommitAsync(cancellationToken);

        return new FieldContextResponse(
            serverDate,
            utcNow,
            [
                .. cycles.Select(c => c with
                {
                    RecordedDates = [.. recorded[c.Id]],
                    LatestStock = [.. latestStock[c.Id].Select(e => new FieldStockEntryResponse(e.Id, e.WeightRangeId, e.Birds, e.WeightKg, e.Notes))]
                })
            ],
            [
                .. items.Select(i => new FieldItemResponse(
                    i.Id,
                    i.Code,
                    i.Name,
                    i.Category,
                    i.BaseUomId,
                    i.BaseUomCode,
                    [
                        new FieldUomResponse(i.BaseUomId, i.BaseUomCode, 1),
                        .. conversions[i.Id].Select(c => new FieldUomResponse(c.UomId, c.Code, c.Factor))
                    ]))
            ],
            stock,
            weightRanges);
    }

    internal sealed record RecordedDateRow(Guid CycleId, DateOnly Date);

    internal sealed record StockEntryRow(Guid CycleId, Guid Id, Guid WeightRangeId, int Birds, decimal WeightKg, string? Notes);

    internal sealed record ItemRow(Guid Id, string Code, string Name, string Category, Guid BaseUomId, string BaseUomCode);

    internal sealed record ConversionRow(Guid ItemId, Guid UomId, string Code, decimal Factor);
}
