using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Documents;
using Application.Users.FieldOfficers;
using Domain.Documents.Attachments;
using Domain.MasterData.Coops;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Coops.Update;

internal sealed class UpdateCoopCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IFieldScope fieldScope,
    IAttachmentService attachments)
    : ICommandHandler<UpdateCoopCommand>
{
    public async Task<Result> Handle(UpdateCoopCommand command, CancellationToken cancellationToken)
    {
        Coop? coop = await context.Coops.SingleOrDefaultAsync(c => c.Id == command.CoopId, cancellationToken);

        if (coop is null || !await fieldScope.CanAccessCoopAsync(coop.Id, cancellationToken))
        {
            return Result.Failure(CoopErrors.NotFound(command.CoopId));
        }

        Result access = await branchAccess.EnsureAccessAsync(coop.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return access;
        }

        FieldScope field = await fieldScope.GetScopeAsync(cancellationToken);
        if (!field.Restricted)
        {
            Result fieldOfficer = await FieldOfficerRules.EnsureValidAsync(
                context, command.FieldOfficerUserId, coop.BranchId, cancellationToken);
            if (fieldOfficer.IsFailure)
            {
                return fieldOfficer;
            }

            coop.AssignFieldOfficer(command.FieldOfficerUserId);
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

        // Clients that do not send the profile (older mobile builds) leave it unchanged.
        if (command.Profile is not null)
        {
            coop.SetProfile(command.Profile);
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
