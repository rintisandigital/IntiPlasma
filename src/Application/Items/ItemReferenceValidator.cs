using Application.Abstractions.Data;
using Domain.MasterData.TaxCodes;
using Domain.MasterData.Uoms;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Items;

/// <summary>
/// Checks that the units and VAT code referenced by an item exist.
/// </summary>
internal static class ItemReferenceValidator
{
    public static async Task<Result> ValidateAsync(
        IApplicationDbContext context,
        IEnumerable<Guid> uomIds,
        Guid? taxCodeId,
        CancellationToken cancellationToken)
    {
        var requestedUomIds = uomIds.Distinct().ToList();

        List<Guid> existingUomIds = await context.Uoms
            .Where(u => requestedUomIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        Guid missingUomId = requestedUomIds.Find(id => !existingUomIds.Contains(id));
        if (missingUomId != Guid.Empty)
        {
            return Result.Failure(UomErrors.NotFound(missingUomId));
        }

        if (taxCodeId is not null &&
            !await context.TaxCodes.AnyAsync(t => t.Id == taxCodeId && t.Type == TaxType.Vat, cancellationToken))
        {
            return Result.Failure(TaxCodeErrors.NotFound(taxCodeId.Value));
        }

        return Result.Success();
    }
}
