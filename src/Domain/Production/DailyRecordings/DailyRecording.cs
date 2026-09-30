using System.Text.Json;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Domain.Production.DailyRecordings;

/// <summary>
/// Recording harian of a cycle, usually entered by the PPL on the mobile app: mortality, culling, average body
/// weight and the feed/OVK used from the coop warehouse. One recording per cycle per day.
/// A recording can be revised with a reason while the cycle is open; every revision keeps a copy of the previous
/// values, so the history is auditable.
/// </summary>
public sealed class DailyRecording : AggregateRoot
{
    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.Web);

    private readonly List<DailyRecordingUsage> _usages = [];
    private readonly List<DailyRecordingRevision> _revisions = [];

    private DailyRecording(Guid id, ProductionCycle cycle, DateOnly date)
        : base(id)
    {
        CycleId = cycle.Id;
        BranchId = cycle.BranchId;
        CoopId = cycle.CoopId;
        Date = date;
        AgeDays = cycle.AgeOn(date);
    }

    private DailyRecording()
    {
    }

    public Guid CycleId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid CoopId { get; private set; }
    public DateOnly Date { get; private set; }

    /// <summary>
    /// Days since chick-in (chick-in day = 0).
    /// </summary>
    public int AgeDays { get; private set; }

    public int Mortality { get; private set; }
    public int Culling { get; private set; }

    /// <summary>
    /// Average body weight in gram, when birds were sampled and weighed that day.
    /// </summary>
    public decimal? AverageBodyWeightGram { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>
    /// 0 for the original entry, incremented by every revision.
    /// </summary>
    public int RevisionNumber { get; private set; }

    public IReadOnlyCollection<DailyRecordingUsage> Usages => [.. _usages];
    public IReadOnlyCollection<DailyRecordingRevision> Revisions => [.. _revisions];

    /// <param name="id">Optional client-generated id (offline mobile entry); retries with the same id are idempotent.</param>
    public static Result<DailyRecording> Create(
        Guid? id,
        ProductionCycle cycle,
        DateOnly date,
        DailyRecordingValues values)
    {
        Result recordable = cycle.EnsureRecordable(date);
        if (recordable.IsFailure)
        {
            return Result.Failure<DailyRecording>(recordable.Error);
        }

        var recording = new DailyRecording(id ?? Guid.CreateVersion7(), cycle, date);

        Result applied = recording.Apply(values);
        if (applied.IsFailure)
        {
            return Result.Failure<DailyRecording>(applied.Error);
        }

        recording.Raise(new DailyRecordingSavedDomainEvent(recording.Id));

        return recording;
    }

    /// <summary>
    /// Replaces the values, keeping the previous values in the revision history.
    /// </summary>
    public Result Revise(DailyRecordingValues values, string reason, Guid? revisedBy, DateTime utcNow)
    {
        string previous = JsonSerializer.Serialize(CurrentValues(), SnapshotOptions);

        Result applied = Apply(values);
        if (applied.IsFailure)
        {
            return applied;
        }

        RevisionNumber++;
        _revisions.Add(new DailyRecordingRevision(Id, RevisionNumber, reason, previous, revisedBy, utcNow));

        Raise(new DailyRecordingSavedDomainEvent(Id));

        return Result.Success();
    }

    /// <summary>
    /// Records the moving average value at which a usage line left the coop warehouse.
    /// </summary>
    public void RecordUsageValue(Guid itemId, Money value)
    {
        _usages.Single(u => u.ItemId == itemId).SetValue(value);
    }

    public DailyRecordingValues CurrentValues() =>
        new(
            Mortality,
            Culling,
            AverageBodyWeightGram,
            Notes,
            [.. _usages.Select(u => new DailyRecordingUsageInput(u.ItemId, u.UomId, u.Quantity, u.BaseQuantity))]);

    private Result Apply(DailyRecordingValues values)
    {
        if (values.Mortality < 0 || values.Culling < 0)
        {
            return Result.Failure(DailyRecordingErrors.NegativeCount);
        }

        if (values.AverageBodyWeightGram is <= 0)
        {
            return Result.Failure(DailyRecordingErrors.InvalidBodyWeight);
        }

        if (values.Usages.Any(u => u.Quantity <= 0 || u.BaseQuantity <= 0) ||
            values.Usages.GroupBy(u => u.ItemId).Any(g => g.Count() > 1))
        {
            return Result.Failure(DailyRecordingErrors.InvalidUsage);
        }

        Mortality = values.Mortality;
        Culling = values.Culling;
        AverageBodyWeightGram = values.AverageBodyWeightGram;
        Notes = values.Notes;

        _usages.Clear();
        _usages.AddRange(values.Usages.Select(u => new DailyRecordingUsage(Id, u.ItemId, u.UomId, u.Quantity, u.BaseQuantity)));

        return Result.Success();
    }
}

public sealed record DailyRecordingValues(
    int Mortality,
    int Culling,
    decimal? AverageBodyWeightGram,
    string? Notes,
    IReadOnlyList<DailyRecordingUsageInput> Usages);

/// <param name="BaseQuantity">Quantity in the item's base unit (kg for feed).</param>
public sealed record DailyRecordingUsageInput(Guid ItemId, Guid UomId, decimal Quantity, decimal BaseQuantity);
