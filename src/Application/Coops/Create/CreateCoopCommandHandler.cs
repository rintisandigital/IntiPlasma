using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Coops.Create;

internal sealed class CreateCoopCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CreateCoopCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCoopCommand command, CancellationToken cancellationToken)
    {
        Farmer? farmer = await context.Farmers.AsNoTracking()
            .SingleOrDefaultAsync(f => f.Id == command.FarmerId, cancellationToken);

        if (farmer is null)
        {
            return Result.Failure<Guid>(FarmerErrors.NotFound(command.FarmerId));
        }

        Result access = await branchAccess.EnsureAccessAsync(farmer.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
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

        if (await context.Coops.AnyAsync(c => c.Code == coop.Value.Code, cancellationToken))
        {
            return Result.Failure<Guid>(CoopErrors.CodeNotUnique(coop.Value.Code));
        }

        context.Coops.Add(coop.Value);

        await context.SaveChangesAsync(cancellationToken);

        return coop.Value.Id;
    }
}
