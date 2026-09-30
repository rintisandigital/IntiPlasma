using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Items;

public static class ItemErrors
{
    public static Error NotFound(Guid itemId) => Error.NotFound(
        "Items.NotFound",
        $"The item with the Id = '{itemId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("Item", code);

    public static Error UnknownUom(Guid itemId, Guid uomId) => Error.Problem(
        "Items.UnknownUom",
        $"The unit '{uomId}' is not defined for the item with the Id = '{itemId}'");

    public static readonly Error ConversionToBaseUom = Error.Problem(
        "Items.ConversionToBaseUom",
        "A conversion cannot use the base unit of the item");

    public static readonly Error DuplicateConversion = Error.Problem(
        "Items.DuplicateConversion",
        "A unit can only be converted once");

    public static readonly Error InvalidConversionFactor = Error.Problem(
        "Items.InvalidConversionFactor",
        "The conversion factor must be greater than zero");
}
