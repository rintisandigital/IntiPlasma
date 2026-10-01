using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Numbering;
using Application.Finance.Journals;
using Domain.Finance.FiscalPeriods;
using Domain.Finance.JournalMappings;
using Domain.Finance.Journals;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.AutoJournal;

internal sealed class AutoJournalService(
    IApplicationDbContext context,
    IDocumentNumberGenerator numberGenerator,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : IAutoJournalService
{
    public async Task<Result<Guid>> PostAsync(AccountingEntry entry, CancellationToken cancellationToken = default)
    {
        Guid? existingId = await FindExistingAsync(entry.EventType, entry.SourceId, cancellationToken);
        if (existingId is not null)
        {
            return existingId.Value;
        }

        Result<IReadOnlyList<JournalLineInput>> lines = await BuildLinesAsync(entry, cancellationToken);
        if (lines.IsFailure)
        {
            return Result.Failure<Guid>(lines.Error);
        }

        return await CreateAndPostAsync(
            entry.EventType, entry.SourceId, entry.BranchId, entry.Date, entry.Description, lines.Value, cancellationToken);
    }

    public async Task<Result<Guid>> PostLinesAsync(
        string sourceType,
        Guid sourceId,
        Guid branchId,
        DateOnly date,
        string description,
        IReadOnlyList<JournalLineInput> lines,
        CancellationToken cancellationToken = default)
    {
        Guid? existingId = await FindExistingAsync(sourceType, sourceId, cancellationToken);
        if (existingId is not null)
        {
            return existingId.Value;
        }

        Result references = await JournalSupport.ValidateReferencesAsync(context, lines, cancellationToken);
        if (references.IsFailure)
        {
            return Result.Failure<Guid>(references.Error);
        }

        return await CreateAndPostAsync(sourceType, sourceId, branchId, date, description, lines, cancellationToken);
    }

    public async Task<Result<Guid>> ReverseAsync(
        string sourceType,
        Guid sourceId,
        DateOnly date,
        string reason,
        CancellationToken cancellationToken = default)
    {
        JournalEntry? journal = await context.JournalEntries
            .Include(j => j.Lines)
            .SingleOrDefaultAsync(j => j.SourceType == sourceType && j.SourceId == sourceId, cancellationToken);

        if (journal is null)
        {
            return Result.Failure<Guid>(JournalErrors.SourceNotJournaled(sourceType, sourceId));
        }

        if (journal.Status == JournalStatus.Reversed)
        {
            return journal.ReversedById!.Value;
        }

        Result<FiscalPeriod> period = await JournalSupport.FindPeriodAsync(context, date, cancellationToken);
        if (period.IsFailure)
        {
            return Result.Failure<Guid>(period.Error);
        }

        string number = await JournalSupport.NextNumberAsync(
            context, numberGenerator, JournalSource.Automatic, journal.BranchId, date, cancellationToken);

        Result<JournalEntry> reversal = journal.Reverse(date, reason, number, period.Value, CurrentUser, dateTimeProvider.UtcNow);
        if (reversal.IsFailure)
        {
            return Result.Failure<Guid>(reversal.Error);
        }

        context.JournalEntries.Add(reversal.Value);

        await context.SaveChangesAsync(cancellationToken);

        return reversal.Value.Id;
    }

    public async Task<Result<IReadOnlyList<JournalLineInput>>> BuildLinesAsync(
        AccountingEntry entry,
        CancellationToken cancellationToken = default)
    {
        List<JournalMapping> candidates = await context.JournalMappings
            .AsNoTracking()
            .Include(m => m.Lines)
            .Where(m => m.EventType == entry.EventType && m.IsActive && (m.BranchId == entry.BranchId || m.BranchId == null))
            .ToListAsync(cancellationToken);

        // A branch-specific mapping wins over the company default.
        JournalMapping? mapping = candidates.Find(m => m.BranchId == entry.BranchId) ?? candidates.Find(m => m.BranchId == null);

        if (mapping is null)
        {
            return Result.Failure<IReadOnlyList<JournalLineInput>>(
                JournalMappingErrors.NotConfigured(entry.EventType, entry.BranchId));
        }

        Result<IReadOnlyList<JournalLineInput>> lines = mapping.BuildLines(entry.Amounts);
        if (lines.IsFailure)
        {
            return lines;
        }

        if (lines.Value.Count == 0)
        {
            return Result.Failure<IReadOnlyList<JournalLineInput>>(JournalMappingErrors.NothingToPost);
        }

        Result references = await JournalSupport.ValidateReferencesAsync(context, lines.Value, cancellationToken);

        return references.IsSuccess ? lines : Result.Failure<IReadOnlyList<JournalLineInput>>(references.Error);
    }

    private Guid? CurrentUser => userContext.IsAuthenticated ? userContext.UserId : null;

    private Task<Guid?> FindExistingAsync(string sourceType, Guid sourceId, CancellationToken cancellationToken) =>
        context.JournalEntries
            .Where(j => j.SourceType == sourceType && j.SourceId == sourceId)
            .Select(j => (Guid?)j.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<Result<Guid>> CreateAndPostAsync(
        string sourceType,
        Guid sourceId,
        Guid branchId,
        DateOnly date,
        string description,
        IReadOnlyList<JournalLineInput> lines,
        CancellationToken cancellationToken)
    {
        Result<JournalEntry> journal = JournalEntry.CreateAutomatic(branchId, date, description, sourceType, sourceId, lines);
        if (journal.IsFailure)
        {
            return Result.Failure<Guid>(journal.Error);
        }

        Result<FiscalPeriod> period = await JournalSupport.FindPeriodAsync(context, date, cancellationToken);
        if (period.IsFailure)
        {
            return Result.Failure<Guid>(period.Error);
        }

        string number = await JournalSupport.NextNumberAsync(
            context, numberGenerator, JournalSource.Automatic, branchId, date, cancellationToken);

        Result posted = journal.Value.Post(number, period.Value, CurrentUser, dateTimeProvider.UtcNow);
        if (posted.IsFailure)
        {
            return Result.Failure<Guid>(posted.Error);
        }

        context.JournalEntries.Add(journal.Value);

        await context.SaveChangesAsync(cancellationToken);

        return journal.Value.Id;
    }
}
