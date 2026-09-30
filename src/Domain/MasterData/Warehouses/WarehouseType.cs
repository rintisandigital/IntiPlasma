namespace Domain.MasterData.Warehouses;

public enum WarehouseType
{
    /// <summary>
    /// Gudang induk (DOC, pakan, OVK).
    /// </summary>
    Central = 1,

    /// <summary>
    /// Gudang kandang, one per coop.
    /// </summary>
    Coop = 2
}
