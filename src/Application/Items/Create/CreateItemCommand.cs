using Application.Abstractions.Messaging;
using Domain.MasterData.Items;

namespace Application.Items.Create;

public sealed record CreateItemCommand(
    string Code,
    string Name,
    ItemCategory Category,
    Guid BaseUomId,
    Guid? TaxCodeId,
    IReadOnlyList<ItemUomConversionRequest> Conversions) : ICommand<Guid>;
