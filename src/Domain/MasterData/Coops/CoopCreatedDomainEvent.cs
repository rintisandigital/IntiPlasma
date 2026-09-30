using SharedKernel;

namespace Domain.MasterData.Coops;

public sealed record CoopCreatedDomainEvent(Guid CoopId) : DomainEvent;
