using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Documents;
using Domain.Documents.Attachments;
using Domain.MasterData.Branches;
using Domain.Partnership.Contracts;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Contracts.Create;

internal sealed class CreateContractCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IAttachmentService attachments)
    : ICommandHandler<CreateContractCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateContractCommand command, CancellationToken cancellationToken)
    {
        Result access = await branchAccess.EnsureAccessAsync(command.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        if (!await context.Branches.AnyAsync(b => b.Id == command.BranchId && b.IsActive, cancellationToken))
        {
            return Result.Failure<Guid>(BranchErrors.NotFound(command.BranchId));
        }

        Result references = await ContractReferenceValidator.ValidateAsync(context, command.Terms, cancellationToken);
        if (references.IsFailure)
        {
            return Result.Failure<Guid>(references.Error);
        }

        Result<PartnershipContract> contract = PartnershipContract.Create(
            command.Code, command.BranchId, command.Scheme, command.Terms.ToDomain());

        if (contract.IsFailure)
        {
            return Result.Failure<Guid>(contract.Error);
        }

        if (await context.Contracts.AnyAsync(c => c.Code == contract.Value.Code, cancellationToken))
        {
            return Result.Failure<Guid>(ContractErrors.CodeNotUnique(contract.Value.Code));
        }

        Result documents = await attachments.ApplyDocumentsAsync(
            contract.Value,
            AttachmentOwner.Of(AttachmentOwnerTypes.Contract, contract.Value.Id),
            contract.Value.BranchId,
            command.Documents,
            cancellationToken);
        if (documents.IsFailure)
        {
            return Result.Failure<Guid>(documents.Error);
        }

        context.Contracts.Add(contract.Value);

        await context.SaveChangesAsync(cancellationToken);

        return contract.Value.Id;
    }
}
