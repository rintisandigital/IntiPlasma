using Application.Abstractions.Messaging;

namespace Application.Contracts.ChangeStatus;

public sealed record ActivateContractCommand(Guid ContractId) : ICommand;

public sealed record DeactivateContractCommand(Guid ContractId) : ICommand;
