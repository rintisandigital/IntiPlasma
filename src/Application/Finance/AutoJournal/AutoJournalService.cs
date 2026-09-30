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
        Guid? existingId = await context.JournalEntries
            .Where(j => j.SourceType == entry.EventType && j.SourceId == entry.SourceId)
            .Select(j => (Guid?)j.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (existingId is not null)
        {
            return existingId.Value;
        }

        Result<IReadOnlyList<JournalLineInput>> lines = await BuildLinesAsync(entry, cancellationToken);
        if (lines.IsFailure)
        {
            return Result.Failure<Guid>(lines.Error);
        }

        Result<JournalEntry> journal = JournalEntry.CreateAutomatic(
            entry.BranchId, entry.Date, entry.Description, entry.EventType, entry.SourceId, lines.Value);

        if (journal.IsFailure)
        {
            return Result.Failure<Guid>(journal.Error);
        }

        Result<FiscalPeriod> period = await JournalSupport.FindPeriodAsync(context, entry.Date, cancellationToken);
        if (period.IsFailure)
        {
            return Result.Failure<Guid>(period.Error);
        }

        string number = await JournalSupport.NextNumberAsync(
            context, numberGenerator, JournalSource.Automatic, entry.BranchId, entry.Date, cancellationToken);

        Guid? postedBy = userContext.IsAuthenticated ? userContext.UserId : null;

        Result posted = journal.Value.Post(number, period.Value, postedBy, dateTimeProvider.UtcNow);
        if (posted.IsFailure)
        {
            return Result.Failure<Guid>(posted.Error);
        }

        context.JournalEntries.Add(journal.Value);

        await context.SaveChangesAsync(cancellationToken);

        return journal.Value.Id;
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
}
