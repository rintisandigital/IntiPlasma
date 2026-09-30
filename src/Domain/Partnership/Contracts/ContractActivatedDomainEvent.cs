using SharedKernel;

namespace Domain.Partnership.Contracts;

public sealed record ContractActivatedDomainEvent(Guid ContractId) : DomainEvent;
