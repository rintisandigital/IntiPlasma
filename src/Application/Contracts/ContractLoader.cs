using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Domain.Partnership.Contracts;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Contracts;

internal static class ContractLoader
{
    /// <summary>
    /// Loads the full contract aggregate (with its terms) and checks the user's branch access.
    /// </summary>
    public static async Task<Result<PartnershipContract>> LoadAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        PartnershipContract? contract = await context.Contracts
            .Include(c => c.InputPrices)
            .Include(c => c.LiveBirdPrices)
            .Include(c => c.Incentives)
            .SingleOrDefaultAsync(c => c.Id == contractId, cancellationToken);

        if (contract is null)
        {
            return Result.Failure<PartnershipContract>(ContractErrors.NotFound(contractId));
        }

        Result access = await branchAccess.EnsureAccessAsync(contract.BranchId, cancellationToken);

        return access.IsSuccess ? contract : Result.Failure<PartnershipContract>(access.Error);
    }
}
