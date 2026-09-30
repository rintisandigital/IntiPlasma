using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Partnership.Contracts;
using SharedKernel;

namespace Application.Contracts.ChangeStatus;

internal sealed class ActivateContractCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<ActivateContractCommand>
{
    public async Task<Result> Handle(ActivateContractCommand command, CancellationToken cancellationToken)
    {
        Result<PartnershipContract> contract = await ContractLoader.LoadAsync(
            context, branchAccess, command.ContractId, cancellationToken);

        if (contract.IsFailure)
        {
            return contract;
        }

        Result result = contract.Value.Activate();
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class DeactivateContractCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<DeactivateContractCommand>
{
    public async Task<Result> Handle(DeactivateContractCommand command, CancellationToken cancellationToken)
    {
        Result<PartnershipContract> contract = await ContractLoader.LoadAsync(
            context, branchAccess, command.ContractId, cancellationToken);

        if (contract.IsFailure)
        {
            return contract;
        }

        Result result = contract.Value.Deactivate();
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
