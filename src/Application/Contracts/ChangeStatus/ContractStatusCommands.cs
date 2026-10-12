using Application.Abstractions.Messaging;

namespace Application.Contracts.ChangeStatus;

/// <summary>
/// Submits a draft or rejected contract for activation through the approval module (PLAN-MOBILE M-85); it becomes
/// active once every level approves. Replaces the former direct activation.
/// </summary>
public sealed record SubmitContractCommand(Guid ContractId) : ICommand<Guid>;

public sealed record DeactivateContractCommand(Guid ContractId) : ICommand;
