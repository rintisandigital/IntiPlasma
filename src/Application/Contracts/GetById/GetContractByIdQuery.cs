using Application.Abstractions.Messaging;

namespace Application.Contracts.GetById;

public sealed record GetContractByIdQuery(Guid ContractId) : IQuery<ContractResponse>;
