using Application.Abstractions.Messaging;

namespace Application.Contracts.Update;

/// <summary>
/// Replaces the terms of a draft contract.
/// </summary>
public sealed record UpdateContractCommand(Guid ContractId, ContractTermsRequest Terms) : ICommand;
