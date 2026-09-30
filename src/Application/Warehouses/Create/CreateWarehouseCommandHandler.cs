using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MasterData.Branches;
using Domain.MasterData.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Warehouses.Create;

internal sealed class CreateWarehouseCommandHandler(IApplicationDbContext context, IBranchAccess branchAccess)
    : ICommandHandler<CreateWarehouseCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateWarehouseCommand command, CancellationToken cancellationToken)
    {
        Result access = await branchAccess.EnsureAccessAsync(command.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        Branch? branch = await context.Branches.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == command.BranchId, cancellationToken);

        if (branch is null)
        {
            return Result.Failure<Guid>(BranchErrors.NotFound(command.BranchId));
        }

        if (!branch.IsActive)
        {
            return Result.Failure<Guid>(BranchErrors.Inactive(branch.Id));
        }

        var warehouse = Warehouse.CreateCentral(command.Code, command.Name, command.BranchId, command.Address);

        if (await context.Warehouses.AnyAsync(w => w.Code == warehouse.Code, cancellationToken))
        {
            return Result.Failure<Guid>(WarehouseErrors.CodeNotUnique(warehouse.Code));
        }

        context.Warehouses.Add(warehouse);

        await context.SaveChangesAsync(cancellationToken);

        return warehouse.Id;
    }
}
