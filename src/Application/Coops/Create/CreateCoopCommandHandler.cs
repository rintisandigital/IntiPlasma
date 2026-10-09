using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Documents;
using Application.Users.FieldOfficers;
using Domain.Documents.Attachments;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Coops.Create;

internal sealed class CreateCoopCommandHandler(
    IApplicationDbContext context,
    IBranchAccess branchAccess,
    IFieldScope fieldScope,
    IAttachmentService attachments)
    : ICommandHandler<CreateCoopCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCoopCommand command, CancellationToken cancellationToken)
    {
        Farmer? farmer = await context.Farmers.AsNoTracking()
            .SingleOrDefaultAsync(f => f.Id == command.FarmerId, cancellationToken);

        if (farmer is null || !await fieldScope.CanAccessFarmerAsync(farmer.Id, cancellationToken))
        {
            return Result.Failure<Guid>(FarmerErrors.NotFound(command.FarmerId));
        }

        Result access = await branchAccess.EnsureAccessAsync(farmer.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        FieldScope field = await fieldScope.GetScopeAsync(cancellationToken);
        Guid? fieldOfficerUserId = field.AssignFieldOfficer(command.FieldOfficerUserId);

        Result fieldOfficer = await FieldOfficerRules.EnsureValidAsync(
            context, fieldOfficerUserId, farmer.BranchId, cancellationToken);
        if (fieldOfficer.IsFailure)
        {
            return Result.Failure<Guid>(fieldOfficer.Error);
        }

        Result<Coop> coop = Coop.Create(
            farmer,
            command.Code,
            command.Name,
            command.Capacity,
            command.HouseType,
            command.Address,
            command.Latitude,
            command.Longitude);

        if (coop.IsFailure)
        {
            return Result.Failure<Guid>(coop.Error);
        }

        coop.Value.SetProfile(command.Profile);
        coop.Value.AssignFieldOfficer(fieldOfficerUserId);

        if (await context.Coops.AnyAsync(c => c.Code == coop.Value.Code, cancellationToken))
        {
            return Result.Failure<Guid>(CoopErrors.CodeNotUnique(coop.Value.Code));
        }

        Result documents = await attachments.ApplyDocumentsAsync(
            coop.Value,
            AttachmentOwner.Of(AttachmentOwnerTypes.Coop, coop.Value.Id),
            coop.Value.BranchId,
            command.Documents,
            cancellationToken);
        if (documents.IsFailure)
        {
            return Result.Failure<Guid>(documents.Error);
        }

        context.Coops.Add(coop.Value);

        await context.SaveChangesAsync(cancellationToken);

        return coop.Value.Id;
    }
}
