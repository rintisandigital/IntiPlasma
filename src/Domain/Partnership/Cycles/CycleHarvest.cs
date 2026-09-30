namespace Domain.Partnership.Cycles;

/// <summary>
/// One harvest (panen) of a cycle: birds caught and weighed on a day, typically one truck load.
/// Sales delivery orders (phase 5) refer to these records.
/// </summary>
public sealed class CycleHarvest
{
    internal CycleHarvest(Guid id, Guid cycleId, DateOnly date, int ageDays, int birds, decimal weightKg, string? notes)
    {
        Id = id;
        CycleId = cycleId;
        Date = date;
        AgeDays = ageDays;
        Birds = birds;
        WeightKg = weightKg;
        Notes = notes;
    }

    private CycleHarvest()
    {
    }

    public Guid Id { get; private set; }
    public Guid CycleId { get; private set; }
    public DateOnly Date { get; private set; }
    public int AgeDays { get; private set; }
    public int Birds { get; private set; }
    public decimal WeightKg { get; private set; }
    public string? Notes { get; private set; }

    public decimal AverageWeightKg => Birds == 0 ? 0 : WeightKg / Birds;
}
