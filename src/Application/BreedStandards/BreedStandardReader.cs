using System.Data.Common;
using System.Text.Json;
using Application.Abstractions.Data;
using Dapper;
using Domain.MasterData.BreedStandards;
using Microsoft.EntityFrameworkCore;

namespace Application.BreedStandards;

/// <summary>
/// The standard a cycle is compared with (M-71): the one frozen at closing, else the cycle's own, else the default.
/// </summary>
/// <param name="IsDefault">The cycle has no standard of its own.</param>
/// <param name="IsFrozen">Taken from the snapshot frozen when the cycle closed.</param>
public sealed record EffectiveStandard(BreedStandardSnapshot Snapshot, bool IsDefault, bool IsFrozen);

/// <summary>
/// Breed standards loaded for read models, by id plus the default one.
/// </summary>
internal sealed class BreedStandardSet(IReadOnlyDictionary<Guid, BreedStandardSnapshot> byId, BreedStandardSnapshot? defaultStandard)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public EffectiveStandard? Resolve(Guid? breedStandardId, string? closingStandardJson)
    {
        if (closingStandardJson is not null
            && JsonSerializer.Deserialize<BreedStandardSnapshot>(closingStandardJson, JsonOptions) is { } frozen)
        {
            return new EffectiveStandard(frozen, breedStandardId is null, IsFrozen: true);
        }

        if (breedStandardId is Guid id)
        {
            return byId.TryGetValue(id, out BreedStandardSnapshot? own) ? new EffectiveStandard(own, false, false) : null;
        }

        return defaultStandard is null ? null : new EffectiveStandard(defaultStandard, true, false);
    }
}

internal static class BreedStandardReader
{
    /// <summary>
    /// Loads the given standards and the default one with their rows.
    /// </summary>
    public static async Task<BreedStandardSet> LoadAsync(
        DbConnection connection,
        IEnumerable<Guid> breedStandardIds,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
    {
        const string sql =
            """
            SELECT s.id AS Id, s.code AS Code, s.name AS Name, s.strain AS Strain, s.is_default AS IsDefault
            FROM master.breed_standards s
            WHERE s.id = ANY(@Ids) OR s.is_default;

            SELECT r.breed_standard_id AS BreedStandardId, r.age_days AS AgeDays, r.body_weight_gram AS BodyWeightGram,
                   r.fcr AS Fcr, r.depletion_percent AS DepletionPercent, r.daily_feed_gram AS DailyFeedGram,
                   r.cumulative_feed_gram AS CumulativeFeedGram
            FROM master.breed_standard_rows r
            JOIN master.breed_standards s ON s.id = r.breed_standard_id
            WHERE s.id = ANY(@Ids) OR s.is_default
            ORDER BY r.breed_standard_id, r.age_days;
            """;

        Guid[] ids = [.. breedStandardIds.Distinct()];

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { Ids = ids }, transaction, cancellationToken: cancellationToken));

        List<HeaderRow> headers = [.. await multi.ReadAsync<HeaderRow>()];
        ILookup<Guid, RowRow> rows = (await multi.ReadAsync<RowRow>()).ToLookup(r => r.BreedStandardId);

        var byId = headers.ToDictionary(
            h => h.Id,
            h => new BreedStandardSnapshot(h.Id, h.Code, h.Name, h.Strain, [.. rows[h.Id].Select(r => r.ToPoint())]));

        HeaderRow? defaultHeader = headers.Find(h => h.IsDefault);

        return new BreedStandardSet(byId, defaultHeader is null ? null : byId[defaultHeader.Id]);
    }

    /// <summary>
    /// The effective standard for a command (closing a cycle, changing its standard): the given one or the default.
    /// </summary>
    public static async Task<BreedStandardSnapshot?> FindEffectiveAsync(
        IApplicationDbContext context,
        Guid? breedStandardId,
        CancellationToken cancellationToken)
    {
        BreedStandard? standard = await context.BreedStandards
            .Include(s => s.Rows)
            .AsNoTracking()
            .Where(s => breedStandardId == null ? s.IsDefault : s.Id == breedStandardId)
            .FirstOrDefaultAsync(cancellationToken);

        return standard?.ToSnapshot();
    }

    internal sealed class HeaderRow
    {
        public Guid Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string? Strain { get; set; }
        public bool IsDefault { get; set; }
    }

    internal sealed class RowRow
    {
        public Guid BreedStandardId { get; set; }
        public int AgeDays { get; set; }
        public decimal BodyWeightGram { get; set; }
        public decimal? Fcr { get; set; }
        public decimal? DepletionPercent { get; set; }
        public decimal? DailyFeedGram { get; set; }
        public decimal? CumulativeFeedGram { get; set; }

        public StandardPoint ToPoint() => new(AgeDays, BodyWeightGram, Fcr, DepletionPercent, DailyFeedGram, CumulativeFeedGram);
    }
}
