using SharedKernel;

namespace Domain.Finance.Journals;

public sealed record JournalPostedDomainEvent(Guid JournalId) : DomainEvent;

public sealed record JournalReversedDomainEvent(Guid JournalId, Guid ReversalJournalId) : DomainEvent;
