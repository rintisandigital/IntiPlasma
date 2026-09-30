using Application.Abstractions.Messaging;

namespace Application.Items.Update;

/// <remarks>
/// The code, category and base unit cannot change once stock and contract prices refer to the item.
/// </remarks>
public sealed record UpdateItemCommand(
    Guid ItemId,
    string Name,
    Guid? TaxCodeId,
    bool IsActive,
    IReadOnlyList<ItemUomConversionRequest> Conversions) : ICommand;
