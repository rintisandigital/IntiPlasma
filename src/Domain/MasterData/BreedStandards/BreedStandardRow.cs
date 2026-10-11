namespace Domain.MasterData.BreedStandards;

/// <summary>
/// The figures of one age of a <see cref="BreedStandard"/>; see <see cref="StandardPoint"/>.
/// </summary>
public sealed class BreedStandardRow
{
    internal BreedStandardRow(Guid breedStandardId, StandardPoint point)
    {
        BreedStandardId = breedStandardId;
        AgeDays = point.AgeDays;
        Set(point);
    }

    private BreedStandardRow()
    {
    }

    public Guid BreedStandardId { get; private set; }
    public int AgeDays { get; private set; }
    public decimal BodyWeightGram { get; private set; }
    public decimal? Fcr { get; private set; }
    public decimal? DepletionPercent { get; private set; }
    public decimal? DailyFeedGram { get; private set; }
    public decimal? CumulativeFeedGram { get; private set; }

    public StandardPoint ToPoint() => new(AgeDays, BodyWeightGram, Fcr, DepletionPercent, DailyFeedGram, CumulativeFeedGram);

    internal void Set(StandardPoint point)
    {
        BodyWeightGram = point.BodyWeightGram;
        Fcr = point.Fcr;
        DepletionPercent = point.DepletionPercent;
        DailyFeedGram = point.DailyFeedGram;
        CumulativeFeedGram = point.CumulativeFeedGram;
    }
}
