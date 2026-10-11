namespace Domain.MasterData.BreedStandards;

/// <summary>
/// A breed standard as one immutable curve: read from the master for running cycles and frozen on the cycle when it
/// closes (M-71), so later changes to the master do not change closed cycles.
/// </summary>
public sealed record BreedStandardSnapshot(
    Guid Id,
    string Code,
    string Name,
    string? Strain,
    IReadOnlyList<StandardPoint> Rows)
{
    private const int Decimals = 3;

    /// <summary>
    /// The standard at an age: the row of that age, or a linear interpolation between the nearest rows before and
    /// after it that have the figure (M-70). Nothing is extrapolated before the first or after the last row.
    /// </summary>
    public StandardValue? ValueAt(int ageDays)
    {
        if (Rows.Count == 0 || ageDays < Rows[0].AgeDays || ageDays > Rows[^1].AgeDays)
        {
            return null;
        }

        return new StandardValue(
            ageDays,
            Interpolate(ageDays, r => r.BodyWeightGram),
            Interpolate(ageDays, r => r.Fcr),
            Interpolate(ageDays, r => r.DepletionPercent),
            Interpolate(ageDays, r => r.CumulativeFeedGram));
    }

    private decimal? Interpolate(int ageDays, Func<StandardPoint, decimal?> figure)
    {
        StandardPoint? before = null;
        StandardPoint? after = null;

        foreach (StandardPoint row in Rows)
        {
            if (figure(row) is null)
            {
                continue;
            }

            if (row.AgeDays <= ageDays)
            {
                before = row;
            }

            if (row.AgeDays >= ageDays)
            {
                after = row;
                break;
            }
        }

        if (before is null || after is null)
        {
            return null;
        }

        decimal from = figure(before)!.Value;
        if (before.AgeDays == after.AgeDays)
        {
            return from;
        }

        decimal to = figure(after)!.Value;
        decimal value = from + (to - from) * (ageDays - before.AgeDays) / (after.AgeDays - before.AgeDays);

        return decimal.Round(value, Decimals, MidpointRounding.AwayFromZero);
    }
}
