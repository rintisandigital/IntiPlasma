using Domain.Common;
using Domain.MasterData.Farmers;
using SharedKernel;

namespace Domain.MasterData.Coops;

/// <summary>
/// Kandang. Belongs to one farmer and hosts many production cycles over time (one at a time).
/// </summary>
public sealed class Coop : AggregateRoot
{
    private Coop(Guid id, string code, Guid farmerId, Guid branchId)
        : base(id)
    {
        Code = code;
        FarmerId = farmerId;
        BranchId = branchId;
        IsActive = true;
    }

    private Coop()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public Guid FarmerId { get; private set; }

    /// <summary>
    /// Copied from the farmer; a coop always belongs to its farmer's branch.
    /// </summary>
    public Guid BranchId { get; private set; }

    /// <summary>
    /// Maximum population (ekor).
    /// </summary>
    public int Capacity { get; private set; }

    public HouseType HouseType { get; private set; }
    public string? Address { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public bool IsActive { get; private set; }

    public static Result<Coop> Create(
        Farmer farmer,
        string code,
        string name,
        int capacity,
        HouseType houseType,
        string? address,
        decimal? latitude,
        decimal? longitude)
    {
        if (!farmer.IsActive)
        {
            return Result.Failure<Coop>(FarmerErrors.Inactive(farmer.Id));
        }

        var coop = new Coop(Guid.CreateVersion7(), Codes.Normalize(code), farmer.Id, farmer.BranchId);

        Result result = coop.Update(name, capacity, houseType, address, latitude, longitude, isActive: true);
        if (result.IsFailure)
        {
            return Result.Failure<Coop>(result.Error);
        }

        coop.Raise(new CoopCreatedDomainEvent(coop.Id));

        return coop;
    }

    public Result Update(
        string name,
        int capacity,
        HouseType houseType,
        string? address,
        decimal? latitude,
        decimal? longitude,
        bool isActive)
    {
        if (capacity <= 0)
        {
            return Result.Failure(CoopErrors.InvalidCapacity);
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return Result.Failure(CoopErrors.InvalidLocation);
        }

        Name = name.Trim();
        Capacity = capacity;
        HouseType = houseType;
        Address = address;
        Latitude = latitude;
        Longitude = longitude;
        IsActive = isActive;

        return Result.Success();
    }
}
