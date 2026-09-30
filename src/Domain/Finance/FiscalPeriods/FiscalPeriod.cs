using SharedKernel;

namespace Domain.Finance.FiscalPeriods;

/// <summary>
/// Monthly accounting period. Journals can only be posted into an open period; closing a period
/// locks its figures for reporting.
/// </summary>
public sealed class FiscalPeriod : AggregateRoot
{
    private FiscalPeriod(Guid id, int year, int month)
        : base(id)
    {
        Year = year;
        Month = month;
        StartDate = new DateOnly(year, month, 1);
        EndDate = StartDate.AddMonths(1).AddDays(-1);
        Status = FiscalPeriodStatus.Open;
    }

    private FiscalPeriod()
    {
    }

    public int Year { get; private set; }
    public int Month { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public FiscalPeriodStatus Status { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public Guid? ClosedBy { get; private set; }

    public bool IsOpen => Status == FiscalPeriodStatus.Open;

    /// <summary>
    /// Creates the twelve monthly periods of a fiscal year (the fiscal year follows the calendar year).
    /// </summary>
    public static IReadOnlyList<FiscalPeriod> CreateYear(int year) =>
        [.. Enumerable.Range(1, 12).Select(month => new FiscalPeriod(Guid.CreateVersion7(), year, month))];

    public bool Contains(DateOnly date) => StartDate <= date && date <= EndDate;

    /// <summary>
    /// The caller checks the cross-aggregate rules: earlier periods are closed and no unposted journals remain.
    /// </summary>
    public Result Close(Guid? userId, DateTime utcNow)
    {
        if (!IsOpen)
        {
            return Result.Failure(FiscalPeriodErrors.AlreadyClosed(Year, Month));
        }

        Status = FiscalPeriodStatus.Closed;
        ClosedAtUtc = utcNow;
        ClosedBy = userId;

        return Result.Success();
    }

    /// <summary>
    /// The caller checks that the following period is still open.
    /// </summary>
    public Result Reopen()
    {
        if (IsOpen)
        {
            return Result.Failure(FiscalPeriodErrors.NotClosed(Year, Month));
        }

        Status = FiscalPeriodStatus.Open;
        ClosedAtUtc = null;
        ClosedBy = null;

        return Result.Success();
    }
}

public enum FiscalPeriodStatus
{
    Open = 1,
    Closed = 2
}
