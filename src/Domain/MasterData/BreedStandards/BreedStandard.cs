using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.BreedStandards;

/// <summary>
/// Breed standard (PLAN-MOBILE M-69): the target growth curve of a broiler strain per day of age — body weight, FCR,
/// depletion and feed intake — that the cycle performance is compared with. Global for all branches, maintained in
/// the WebApp. One active standard is the default, used by cycles without a standard of their own (M-71).
/// Standards are deactivated, never deleted.
/// </summary>
public sealed class BreedStandard : AggregateRoot
{
    public const int MaxAgeDays = 70;

    private readonly List<BreedStandardRow> _rows = [];

    private BreedStandard(Guid id, string code, string name, string? strain, string? notes)
        : base(id)
    {
        Code = code;
        Name = name;
        Strain = strain;
        Notes = notes;
        IsActive = true;
    }

    private BreedStandard()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }

    /// <summary>
    /// Free text, e.g. "CP 707" or "Cobb 500".
    /// </summary>
    public string? Strain { get; private set; }

    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>
    /// Used by every cycle that has no standard of its own; the caller keeps it to one standard.
    /// </summary>
    public bool IsDefault { get; private set; }

    public IReadOnlyCollection<BreedStandardRow> Rows => [.. _rows.OrderBy(r => r.AgeDays)];

    public static Result<BreedStandard> Create(
        string code,
        string name,
        string? strain,
        string? notes,
        IEnumerable<StandardPoint> rows)
    {
        var standard = new BreedStandard(Guid.CreateVersion7(), Codes.Normalize(code), name.Trim(), Clean(strain), Clean(notes));

        Result result = standard.ReplaceRows(rows);

        return result.IsSuccess ? standard : Result.Failure<BreedStandard>(result.Error);
    }

    public Result Update(string name, string? strain, string? notes, bool isActive)
    {
        if (IsDefault && !isActive)
        {
            return Result.Failure(BreedStandardErrors.DefaultMustBeActive);
        }

        Name = name.Trim();
        Strain = Clean(strain);
        Notes = Clean(notes);
        IsActive = isActive;

        return Result.Success();
    }

    public Result MakeDefault()
    {
        if (!IsActive)
        {
            return Result.Failure(BreedStandardErrors.Inactive(Code));
        }

        IsDefault = true;

        return Result.Success();
    }

    public void ClearDefault() => IsDefault = false;

    /// <summary>
    /// Replaces the age rows (M-69, M-73). Ages are 0-70 and unique; body weight is required and, like depletion
    /// and cumulative feed, never decreases with age. When no row has a cumulative feed but every row has a daily
    /// feed and the ages run day by day, the cumulative feed is the running total of the daily feed.
    /// Existing rows are updated in place so their keys stay tracked.
    /// </summary>
    public Result ReplaceRows(IEnumerable<StandardPoint> rows)
    {
        var points = rows.OrderBy(r => r.AgeDays).ToList();

        Result valid = Validate(points);
        if (valid.IsFailure)
        {
            return valid;
        }

        points = WithCumulativeFeed(points);

        _rows.RemoveAll(r => !points.Exists(p => p.AgeDays == r.AgeDays));

        foreach (StandardPoint point in points)
        {
            BreedStandardRow? row = _rows.Find(r => r.AgeDays == point.AgeDays);
            if (row is null)
            {
                _rows.Add(new BreedStandardRow(Id, point));
            }
            else
            {
                row.Set(point);
            }
        }

        return Result.Success();
    }

    public BreedStandardSnapshot ToSnapshot() => new(Id, Code, Name, Strain, [.. Rows.Select(r => r.ToPoint())]);

    private static Result Validate(List<StandardPoint> points)
    {
        if (points.Count == 0)
        {
            return Result.Failure(BreedStandardErrors.NoRows);
        }

        StandardPoint? previous = null;

        foreach (StandardPoint point in points)
        {
            if (point.AgeDays is < 0 or > MaxAgeDays)
            {
                return Result.Failure(BreedStandardErrors.InvalidAge(point.AgeDays));
            }

            if (previous?.AgeDays == point.AgeDays)
            {
                return Result.Failure(BreedStandardErrors.DuplicateAge(point.AgeDays));
            }

            if (point.BodyWeightGram <= 0
                || point.Fcr <= 0
                || point.DepletionPercent is < 0 or > 100
                || point.DailyFeedGram < 0
                || point.CumulativeFeedGram < 0)
            {
                return Result.Failure(BreedStandardErrors.InvalidValue(point.AgeDays));
            }

            string? decreasing = previous is null ? null : DecreasingFigure(previous, point, points);
            if (decreasing is not null)
            {
                return Result.Failure(BreedStandardErrors.Decreasing(point.AgeDays, decreasing));
            }

            previous = point;
        }

        return Result.Success();
    }

    private static string? DecreasingFigure(StandardPoint previous, StandardPoint point, List<StandardPoint> points)
    {
        if (point.BodyWeightGram < previous.BodyWeightGram)
        {
            return "body weight";
        }

        if (point.DepletionPercent < LastBefore(points, point, p => p.DepletionPercent))
        {
            return "depletion";
        }

        return point.CumulativeFeedGram < LastBefore(points, point, p => p.CumulativeFeedGram) ? "cumulative feed" : null;
    }

    private static decimal? LastBefore(List<StandardPoint> points, StandardPoint point, Func<StandardPoint, decimal?> figure) =>
        points.LastOrDefault(p => p.AgeDays < point.AgeDays && figure(p) is not null) is { } earlier ? figure(earlier) : null;

    private static List<StandardPoint> WithCumulativeFeed(List<StandardPoint> points)
    {
        bool derive = points.TrueForAll(p => p.CumulativeFeedGram is null && p.DailyFeedGram is not null)
            && points[^1].AgeDays - points[0].AgeDays == points.Count - 1;

        if (!derive)
        {
            return points;
        }

        decimal total = 0;

        return
        [
            .. points.Select(p =>
            {
                total += p.DailyFeedGram!.Value;
                return p with { CumulativeFeedGram = total };
            })
        ];
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
