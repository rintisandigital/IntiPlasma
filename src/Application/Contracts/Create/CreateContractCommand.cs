using Application.Abstractions.Messaging;
using Domain.Partnership.Contracts;

namespace Application.Contracts.Create;

public sealed record CreateContractCommand(
    string Code,
    Guid BranchId,
    ContractScheme Scheme,
    ContractTermsRequest Terms,
    IReadOnlyList<Guid>? Documents = null) : ICommand<Guid>;
