using SharedKernel;

namespace Domain.Partnership.Cycles;

public sealed record CyclePlannedDomainEvent(Guid CycleId) : DomainEvent;

public sealed record CycleStartedDomainEvent(Guid CycleId) : DomainEvent;

public sealed record CycleCancelledDomainEvent(Guid CycleId) : DomainEvent;
