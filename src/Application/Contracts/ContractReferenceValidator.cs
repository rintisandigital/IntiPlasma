using Application.Abstractions.Data;
using Domain.MasterData.Items;
using Domain.MasterData.TaxCodes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Contracts;

internal static class ContractReferenceValidator
{
    private static readonly ItemCategory[] SapronakCategories = [ItemCategory.Doc, ItemCategory.Feed, ItemCategory.Ovk];

    /// <summary>
    /// Contract prices may only be set for existing sapronak items (DOC, feed, OVK), and the withholding
    /// tax must be an income tax (PPh) code.
    /// </summary>
    public static async Task<Result> ValidateAsync(
        IApplicationDbContext context,
        ContractTermsRequest terms,
        CancellationToken cancellationToken)
    {
        var itemIds = terms.InputPrices.Select(p => p.ItemId).Distinct().ToList();

        List<Guid> sapronakIds = await context.Items
            .Where(i => itemIds.Contains(i.Id) && SapronakCategories.Contains(i.Category))
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

        Guid invalidItemId = itemIds.Find(id => !sapronakIds.Contains(id));
        if (invalidItemId != Guid.Empty)
        {
            return Result.Failure(ItemErrors.NotFound(invalidItemId));
        }

        if (terms.IncomeTaxCodeId is Guid taxCodeId &&
            !await context.TaxCodes.AnyAsync(t => t.Id == taxCodeId && t.Type == TaxType.IncomeTax, cancellationToken))
        {
            return Result.Failure(TaxCodeErrors.NotIncomeTax(taxCodeId));
        }

        return Result.Success();
    }
}
