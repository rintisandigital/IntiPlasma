using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Documents;
using Domain.Documents.Attachments;
using Domain.Partnership.Contracts;
using SharedKernel;

namespace Application.Contracts.Update;

internal sealed class UpdateContractCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IAttachmentService attachments)
    : ICommandHandler<UpdateContractCommand>
{
    public async Task<Result> Handle(UpdateContractCommand command, CancellationToken cancellationToken)
    {
        Result<PartnershipContract> contract = await ContractLoader.LoadAsync(
            context, branchAccess, command.ContractId, cancellationToken);

        if (contract.IsFailure)
        {
            return contract;
        }

        Result references = await ContractReferenceValidator.ValidateAsync(context, command.Terms, cancellationToken);
        if (references.IsFailure)
        {
            return references;
        }

        Result result = contract.Value.UpdateTerms(command.Terms.ToDomain());
        if (result.IsFailure)
        {
            return result;
        }

        Result documents = await attachments.ApplyDocumentsAsync(
            contract.Value,
            AttachmentOwner.Of(AttachmentOwnerTypes.Contract, contract.Value.Id),
            contract.Value.BranchId,
            command.Documents,
            cancellationToken);
        if (documents.IsFailure)
        {
            return documents;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
