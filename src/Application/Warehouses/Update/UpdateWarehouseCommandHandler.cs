using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MasterData.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Warehouses.Update;

internal sealed class UpdateWarehouseCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<UpdateWarehouseCommand>
{
    public async Task<Result> Handle(UpdateWarehouseCommand command, CancellationToken cancellationToken)
    {
        Warehouse? warehouse = await context.Warehouses
            .SingleOrDefaultAsync(w => w.Id == command.WarehouseId, cancellationToken);

        if (warehouse is null)
        {
            return Result.Failure(WarehouseErrors.NotFound(command.WarehouseId));
        }

        Result access = await branchAccess.EnsureAccessAsync(warehouse.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return access;
        }

        warehouse.Update(command.Name, command.Address, command.IsActive);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
