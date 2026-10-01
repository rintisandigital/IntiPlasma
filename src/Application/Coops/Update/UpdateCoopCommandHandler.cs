using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Documents;
using Domain.Documents.Attachments;
using Domain.MasterData.Coops;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Coops.Update;

internal sealed class UpdateCoopCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IAttachmentService attachments)
    : ICommandHandler<UpdateCoopCommand>
{
    public async Task<Result> Handle(UpdateCoopCommand command, CancellationToken cancellationToken)
    {
        Coop? coop = await context.Coops.SingleOrDefaultAsync(c => c.Id == command.CoopId, cancellationToken);

        if (coop is null)
        {
            return Result.Failure(CoopErrors.NotFound(command.CoopId));
        }

        Result access = await branchAccess.EnsureAccessAsync(coop.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return access;
        }

        Result result = coop.Update(
            command.Name,
            command.Capacity,
            command.HouseType,
            command.Address,
            command.Latitude,
            command.Longitude,
            command.IsActive);

        if (result.IsFailure)
        {
            return result;
        }

        Result documents = await attachments.ApplyDocumentsAsync(
            coop,
            AttachmentOwner.Of(AttachmentOwnerTypes.Coop, coop.Id),
            coop.BranchId,
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
