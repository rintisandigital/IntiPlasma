using System.Data.Common;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Dapper;
using Domain.Finance.FiscalPeriods;
using Domain.Finance.Journals;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.FiscalPeriods;

public sealed record OpenFiscalYearCommand(int Year) : ICommand<int>;

public sealed record CloseFiscalPeriodCommand(Guid FiscalPeriodId) : ICommand;

public sealed record ReopenFiscalPeriodCommand(Guid FiscalPeriodId) : ICommand;

public sealed record GetFiscalPeriodsQuery(int? Year) : IQuery<IReadOnlyList<FiscalPeriodResponse>>;

public sealed record FiscalPeriodResponse
{
    public Guid Id { get; init; }

    public int Year { get; init; }

    public int Month { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly EndDate { get; init; }

    public string Status { get; init; }

    public DateTime? ClosedAtUtc { get; init; }
}

internal sealed class OpenFiscalYearCommandValidator : AbstractValidator<OpenFiscalYearCommand>
{
    public OpenFiscalYearCommandValidator()
    {
        RuleFor(c => c.Year).InclusiveBetween(2000, 2100);
    }
}

internal sealed class OpenFiscalYearCommandHandler(IApplicationDbContext context)
    : ICommandHandler<OpenFiscalYearCommand, int>
{
    public async Task<Result<int>> Handle(OpenFiscalYearCommand command, CancellationToken cancellationToken)
    {
        if (await context.FiscalPeriods.AnyAsync(p => p.Year == command.Year, cancellationToken))
        {
            return Result.Failure<int>(FiscalPeriodErrors.YearAlreadyExists(command.Year));
        }

        IReadOnlyList<FiscalPeriod> periods = FiscalPeriod.CreateYear(command.Year);

        context.FiscalPeriods.AddRange(periods);

        await context.SaveChangesAsync(cancellationToken);

        return periods.Count;
    }
}

/// <summary>
/// Closes a period after the blocking checks of the closing checklist. Closing December also closes the year:
/// the closing journals (revenue and expenses to retained earnings) are posted in the same transaction.
/// </summary>
internal sealed class CloseFiscalPeriodCommandHandler(
    IApplicationDbContext context,
    IDbConnectionFactory dbConnectionFactory,
    IDocumentNumberGenerator numberGenerator,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<CloseFiscalPeriodCommand>
{
    private static readonly JournalStatus[] UnpostedStatuses = [JournalStatus.Draft, JournalStatus.Approved];

    public async Task<Result> Handle(CloseFiscalPeriodCommand command, CancellationToken cancellationToken)
    {
        FiscalPeriod? period = await context.FiscalPeriods
            .SingleOrDefaultAsync(p => p.Id == command.FiscalPeriodId, cancellationToken);

        if (period is null)
        {
            return Result.Failure(FiscalPeriodErrors.NotFound(command.FiscalPeriodId));
        }

        FiscalPeriod? earlierOpen = await context.FiscalPeriods
            .Where(p => p.StartDate < period.StartDate && p.Status == FiscalPeriodStatus.Open)
            .OrderBy(p => p.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (earlierOpen is not null)
        {
            return Result.Failure(FiscalPeriodErrors.PreviousPeriodOpen(earlierOpen.Year, earlierOpen.Month));
        }

        int unposted = await context.JournalEntries.CountAsync(
            j => UnpostedStatuses.Contains(j.Status) && j.Date >= period.StartDate && j.Date <= period.EndDate,
            cancellationToken);

        if (unposted > 0)
        {
            return Result.Failure(FiscalPeriodErrors.HasUnpostedJournals(unposted));
        }

        // Automatic journals still in (or dead in) the outbox would be missing from the closed period.
        IReadOnlyList<ClosingCheck> checks = await PeriodClosingChecks.RunAsync(dbConnectionFactory, period, cancellationToken);
        int autoJournalProblems = checks.Where(c => c.Blocking && c.Code != "UnpostedJournals").Sum(c => c.Count);
        if (autoJournalProblems > 0)
        {
            return Result.Failure(FiscalPeriodErrors.AutoJournalsNotPosted(autoJournalProblems));
        }

        if (period.Month == 12)
        {
            Result<IReadOnlyList<JournalEntry>> closing = await YearEndClosing.CreateClosingJournalsAsync(
                context, numberGenerator, period, userContext.UserId, dateTimeProvider.UtcNow, cancellationToken);

            if (closing.IsFailure)
            {
                return closing;
            }

            context.JournalEntries.AddRange(closing.Value);
        }

        Result result = period.Close(userContext.UserId, dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>
/// Reopens the last closed period. Reopening December reverses the year's closing journals.
/// </summary>
internal sealed class ReopenFiscalPeriodCommandHandler(
    IApplicationDbContext context,
    IDocumentNumberGenerator numberGenerator,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ReopenFiscalPeriodCommand>
{
    public async Task<Result> Handle(ReopenFiscalPeriodCommand command, CancellationToken cancellationToken)
    {
        FiscalPeriod? period = await context.FiscalPeriods
            .SingleOrDefaultAsync(p => p.Id == command.FiscalPeriodId, cancellationToken);

        if (period is null)
        {
            return Result.Failure(FiscalPeriodErrors.NotFound(command.FiscalPeriodId));
        }

        FiscalPeriod? laterClosed = await context.FiscalPeriods
            .Where(p => p.StartDate > period.StartDate && p.Status == FiscalPeriodStatus.Closed)
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (laterClosed is not null)
        {
            return Result.Failure(FiscalPeriodErrors.NextPeriodClosed(laterClosed.Year, laterClosed.Month));
        }

        Result result = period.Reopen();
        if (result.IsFailure)
        {
            return result;
        }

        if (period.Month == 12)
        {
            Result<IReadOnlyList<JournalEntry>> reversals = await YearEndClosing.ReverseClosingJournalsAsync(
                context, numberGenerator, period, userContext.UserId, dateTimeProvider.UtcNow, cancellationToken);

            if (reversals.IsFailure)
            {
                return reversals;
            }

            context.JournalEntries.AddRange(reversals.Value);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class GetFiscalPeriodsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetFiscalPeriodsQuery, IReadOnlyList<FiscalPeriodResponse>>
{
    public async Task<Result<IReadOnlyList<FiscalPeriodResponse>>> Handle(
        GetFiscalPeriodsQuery query,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        IEnumerable<FiscalPeriodResponse> periods = await connection.QueryAsync<FiscalPeriodResponse>(new CommandDefinition(
            """
            SELECT p.id AS Id, p.year AS Year, p.month AS Month, p.start_date AS StartDate, p.end_date AS EndDate,
                   p.status AS Status, p.closed_at_utc AS ClosedAtUtc
            FROM finance.fiscal_periods p
            WHERE (@Year::int IS NULL OR p.year = @Year)
            ORDER BY p.start_date
            """,
            new { query.Year },
            cancellationToken: cancellationToken));

        return periods.ToList();
    }
}
