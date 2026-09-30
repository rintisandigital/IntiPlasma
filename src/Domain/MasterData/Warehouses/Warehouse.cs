using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Warehouses;

/// <summary>
/// A stock location. Central warehouses (gudang induk) receive purchased sapronak; every coop has its own
/// coop warehouse (gudang kandang) that receives transfers for the running production cycle.
/// </summary>
public sealed class Warehouse : AggregateRoot
{
    private Warehouse(Guid id, string code, string name, Guid branchId, WarehouseType type, Guid? coopId, string? address)
        : base(id)
    {
        Code = code;
        Name = name;
        BranchId = branchId;
        Type = type;
        CoopId = coopId;
        Address = address;
        IsActive = true;
    }

    private Warehouse()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public Guid BranchId { get; private set; }
    public WarehouseType Type { get; private set; }

    /// <summary>
    /// Set only for <see cref="WarehouseType.Coop"/> warehouses.
    /// </summary>
    public Guid? CoopId { get; private set; }

    public string? Address { get; private set; }
    public bool IsActive { get; private set; }

    public static Warehouse CreateCentral(string code, string name, Guid branchId, string? address) =>
        new(Guid.CreateVersion7(), Codes.Normalize(code), name.Trim(), branchId, WarehouseType.Central, null, address);

    public static Warehouse CreateForCoop(string code, string name, Guid branchId, Guid coopId, string? address) =>
        new(Guid.CreateVersion7(), Codes.Normalize(code), name.Trim(), branchId, WarehouseType.Coop, coopId, address);

    public void Update(string name, string? address, bool isActive)
    {
        Name = name.Trim();
        Address = address;
        IsActive = isActive;
    }
}
