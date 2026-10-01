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

    /// <summary>
    /// Posts an automatic journal whose lines are given by the source document itself (e.g. a cash-out naming its
    /// expense accounts) instead of a mapping. Idempotent per (source type, source id) like <see cref="PostAsync"/>.
    /// </summary>
    Task<Result<Guid>> PostLinesAsync(
        string sourceType,
        Guid sourceId,
        Guid branchId,
        DateOnly date,
        string description,
        IReadOnlyList<JournalLineInput> lines,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverses the automatic journal of (source type, source id) on a date, e.g. when the source document is voided.
    /// Idempotent: an already reversed journal returns its reversal.
    /// </summary>
    Task<Result<Guid>> ReverseAsync(
        string sourceType,
        Guid sourceId,
        DateOnly date,
        string reason,
        CancellationToken cancellationToken = default);
}
