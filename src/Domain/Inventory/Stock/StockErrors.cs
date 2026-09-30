using System.Globalization;
using SharedKernel;

namespace Domain.Inventory.Stock;

public static class StockErrors
{
    public static Error InsufficientStock(Guid itemId, Guid warehouseId, decimal available, decimal requested) => Error.Problem(
        "Stock.Insufficient",
        string.Create(CultureInfo.InvariantCulture, $"Item '{itemId}' in warehouse '{warehouseId}' has {available:0.####} available but {requested:0.####} was requested"));

    public static readonly Error InvalidQuantity = Error.Problem(
        "Stock.InvalidQuantity",
        "The quantity must be greater than zero and the cost cannot be negative");
}
