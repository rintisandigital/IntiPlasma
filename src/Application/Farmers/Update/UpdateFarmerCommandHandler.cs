using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Documents;
using Domain.Common;
using Domain.Documents.Attachments;
using Domain.MasterData.Farmers;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Farmers.Update;

internal sealed class UpdateFarmerCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IAttachmentService attachments)
    : ICommandHandler<UpdateFarmerCommand>
{
    public async Task<Result> Handle(UpdateFarmerCommand command, CancellationToken cancellationToken)
    {
        Farmer? farmer = await context.Farmers.SingleOrDefaultAsync(f => f.Id == command.FarmerId, cancellationToken);

        if (farmer is null)
        {
            return Result.Failure(FarmerErrors.NotFound(command.FarmerId));
        }

        Result access = await branchAccess.EnsureAccessAsync(farmer.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return access;
        }

        Result<TaxIdentity> taxIdentity = command.TaxIdentity.ToDomain();
        if (taxIdentity.IsFailure)
        {
            return taxIdentity;
        }

        Result<BankAccount> bankAccount = command.BankAccount.ToDomain();
        if (bankAccount.IsFailure)
        {
            return bankAccount;
        }

        Result result = farmer.Update(
            command.Name,
            command.Nik,
            taxIdentity.Value,
            command.Address,
            command.Phone,
            bankAccount.Value,
            command.IsActive);

        if (result.IsFailure)
        {
            return result;
        }

        Result documents = await attachments.ApplyDocumentsAsync(
            farmer,
            AttachmentOwner.Of(AttachmentOwnerTypes.Farmer, farmer.Id),
            farmer.BranchId,
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
