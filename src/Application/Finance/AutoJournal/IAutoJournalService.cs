using Domain.Finance.JournalMappings;
using Domain.Finance.Journals;
using SharedKernel;

namespace Application.Finance.AutoJournal;

/// <summary>
/// An operational transaction expressed as amounts per component of an accounting event.
/// </summary>
/// <param name="EventType">A code from <see cref="AccountingEvents"/>.</param>
/// <param name="SourceId">Id of the source document; together with the event type it makes posting idempotent.</param>
public sealed record AccountingEntry(
    string EventType,
    Guid SourceId,
    Guid BranchId,
    DateOnly Date,
    string Description,
    IReadOnlyList<AccountingAmount> Amounts);

/// <summary>
/// Auto journal engine. Operational modules (procurement, inventory, sales, settlement) call it from their
/// domain event handlers, which run from the outbox, so every transaction gets its journal exactly once.
/// </summary>
public interface IAutoJournalService
{
    /// <summary>
    /// Builds, validates and posts the journal. Posting the same (event type, source id) again returns the
    /// existing journal instead of creating a duplicate.
    /// </summary>
    Task<Result<Guid>> PostAsync(AccountingEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the journal lines without saving anything (used to check a mapping).
    /// </summary>
    Task<Result<IReadOnlyList<JournalLineInput>>> BuildLinesAsync(AccountingEntry entry, CancellationToken cancellationToken = default);
}
