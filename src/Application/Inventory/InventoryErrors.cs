using SharedKernel;

namespace Application.Inventory;

internal static class InventoryErrors
{
    public static Error CoopWarehouseMissing(Guid coopId) => Error.Problem(
        "Inventory.CoopWarehouseMissing",
        $"The coop '{coopId}' has no coop warehouse yet (it is created shortly after the coop); please retry");

    public static readonly Error ChickInNeedsDoc = Error.Problem(
        "Inventory.ChickInNeedsDoc",
        "Chick-in lines must be distinct DOC items");
}
