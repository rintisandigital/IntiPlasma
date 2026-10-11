using Application.Abstractions.Data;
using Domain.MasterData.BreedStandards;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cycles;

internal static class CycleStandardRules
{
    /// <summary>
    /// A cycle can only choose an existing, active breed standard; null (the default) is always allowed.
    /// </summary>
    public static async Task<Result> EnsureUsableAsync(
        IApplicationDbContext context,
        Guid? breedStandardId,
        CancellationToken cancellationToken)
    {
        if (breedStandardId is not Guid id)
        {
            return Result.Success();
        }

        var standard = await context.BreedStandards
            .Where(s => s.Id == id)
            .Select(s => new { s.Code, s.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (standard is null)
        {
            return Result.Failure(BreedStandardErrors.NotFound(id));
        }

        return standard.IsActive ? Result.Success() : Result.Failure(BreedStandardErrors.Inactive(standard.Code));
    }
}
