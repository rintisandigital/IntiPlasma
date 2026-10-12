using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Approvals;
using Domain.Approvals;
using Domain.Partnership.Contracts;
using SharedKernel;

namespace Application.Contracts.ChangeStatus;

internal sealed class SubmitContractCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IApprovalService approvals) : ICommandHandler<SubmitContractCommand, Guid>
{
    public async Task<Result<Guid>> Handle(SubmitContractCommand command, CancellationToken cancellationToken)
    {
        Result<PartnershipContract> contract = await ContractLoader.LoadAsync(
            context, branchAccess, command.ContractId, cancellationToken);

        if (contract.IsFailure)
        {
            return Result.Failure<Guid>(contract.Error);
        }

        Result submitted = contract.Value.Submit();
        if (submitted.IsFailure)
        {
            return Result.Failure<Guid>(submitted.Error);
        }

        Result<ApprovalRequest> request = await approvals.SubmitAsync(
            ApprovalSubmissions.ForContract(contract.Value), cancellationToken);
        if (request.IsFailure)
        {
            return Result.Failure<Guid>(request.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return request.Value.Id;
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
