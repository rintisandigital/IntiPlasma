using Application.Abstractions.Messaging;
using Application.Items;
using Application.Items.GetById;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;

namespace Web.App.Infrastructure;

/// <summary>
/// Item details for document line forms: the label of a chosen item and the units it can be entered in, so a
/// re-rendered form (validation error, edit) shows the same choices as the client-side unit list (~/js/item-units.js).
/// </summary>
public sealed class ItemOptions(IQueryHandler<GetItemByIdQuery, ItemResponse> itemQuery)
{
    private readonly Dictionary<Guid, ItemResponse?> _cache = [];

    public async Task<string?> LabelAsync(Guid? itemId, CancellationToken cancellationToken) =>
        await GetAsync(itemId, cancellationToken) is { } item ? $"{item.Code} — {item.Name} ({item.BaseUomCode})" : null;

    public async Task<IReadOnlyList<SelectListItem>> UnitsAsync(Guid? itemId, Guid? selected, CancellationToken cancellationToken)
    {
        if (await GetAsync(itemId, cancellationToken) is not { } item)
        {
            return [];
        }

        return
        [
            new SelectListItem(item.BaseUomCode, item.BaseUomId.ToString(), selected is null || selected == item.BaseUomId),
            .. item.Conversions.Select(c => new SelectListItem(
                $"{c.UomCode} (= {c.Factor:0.######} {item.BaseUomCode})", c.UomId.ToString(), c.UomId == selected))
        ];
    }

    private async Task<ItemResponse?> GetAsync(Guid? itemId, CancellationToken cancellationToken)
    {
        if (itemId is not Guid id)
        {
            return null;
        }

        if (!_cache.TryGetValue(id, out ItemResponse? item))
        {
            Result<ItemResponse> result = await itemQuery.Handle(new GetItemByIdQuery(id), cancellationToken);
            item = result.IsSuccess ? result.Value : null;
            _cache[id] = item;
        }

        return item;
    }
}
