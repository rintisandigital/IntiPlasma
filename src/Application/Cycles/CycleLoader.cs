using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Domain.Partnership.Cycles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cycles;

internal static class CycleLoader
{
    public static async Task<Result<ProductionCycle>> LoadAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        ProductionCycle? cycle = await context.ProductionCycles
            .SingleOrDefaultAsync(c => c.Id == cycleId, cancellationToken);

        if (cycle is null)
        {
            return Result.Failure<ProductionCycle>(CycleErrors.NotFound(cycleId));
        }

        Result access = await branchAccess.EnsureAccessAsync(cycle.BranchId, cancellationToken);

        return access.IsSuccess ? cycle : Result.Failure<ProductionCycle>(access.Error);
    }
}
