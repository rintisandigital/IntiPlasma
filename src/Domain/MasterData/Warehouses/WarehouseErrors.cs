using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Warehouses;

public static class WarehouseErrors
{
    public static Error NotFound(Guid warehouseId) => Error.NotFound(
        "Warehouses.NotFound",
        $"The warehouse with the Id = '{warehouseId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("Warehouse", code);
}
