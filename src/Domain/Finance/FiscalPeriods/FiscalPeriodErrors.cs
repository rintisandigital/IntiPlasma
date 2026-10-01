using SharedKernel;

namespace Domain.Finance.FiscalPeriods;

public static class FiscalPeriodErrors
{
    public static Error NotFound(Guid periodId) => Error.NotFound(
        "FiscalPeriods.NotFound",
        $"The fiscal period with the Id = '{periodId}' was not found");

    public static Error NotFoundForDate(DateOnly date) => Error.Problem(
        "FiscalPeriods.NotFoundForDate",
        $"No fiscal period exists for {date:yyyy-MM-dd}; open the fiscal year first");

    public static Error YearAlreadyExists(int year) => Error.Conflict(
        "FiscalPeriods.YearAlreadyExists",
        $"The fiscal year {year} already exists");

    public static Error Closed(DateOnly date) => Error.Problem(
        "FiscalPeriods.Closed",
        $"The fiscal period of {date:yyyy-MM-dd} is closed");

    public static Error AlreadyClosed(int year, int month) => Error.Problem(
        "FiscalPeriods.AlreadyClosed",
        $"The fiscal period {year}-{month:D2} is already closed");

    public static Error NotClosed(int year, int month) => Error.Problem(
        "FiscalPeriods.NotClosed",
        $"The fiscal period {year}-{month:D2} is not closed");

    public static Error PreviousPeriodOpen(int year, int month) => Error.Problem(
        "FiscalPeriods.PreviousPeriodOpen",
        $"Close the earlier fiscal period {year}-{month:D2} first");

    public static Error NextPeriodClosed(int year, int month) => Error.Problem(
        "FiscalPeriods.NextPeriodClosed",
        $"Reopen the later fiscal period {year}-{month:D2} first");

    public static Error HasUnpostedJournals(int count) => Error.Problem(
        "FiscalPeriods.HasUnpostedJournals",
        $"The period still has {count} draft or approved journal(s); post or delete them before closing");

    public static Error AutoJournalsNotPosted(int count) => Error.Problem(
        "FiscalPeriods.AutoJournalsNotPosted",
        $"{count} automatic journal event(s) are pending or failed; see the closing checklist and retry failed events before closing");
}
