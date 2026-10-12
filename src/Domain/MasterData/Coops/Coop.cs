using Domain.Common;
using Domain.MasterData.Farmers;
using SharedKernel;

namespace Domain.MasterData.Coops;

/// <summary>
/// Kandang. Belongs to one farmer and hosts many production cycles over time (one at a time).
/// </summary>
public sealed class Coop : AggregateRoot, IHasDocuments
{
    private Coop(Guid id, string code, Guid farmerId, Guid branchId)
        : base(id)
    {
        Code = code;
        FarmerId = farmerId;
        BranchId = branchId;
        RegistrationStatus = RegistrationStatus.Draft;
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

    /// <summary>
    /// Only true once the coop is approved (M-81).
    /// </summary>
    public bool IsActive { get; private set; }

    public RegistrationStatus RegistrationStatus { get; private set; }

    /// <summary>
    /// PPL (petugas penyuluh lapangan) responsible for this coop. Users limited to their assigned data
    /// (<c>partnership:assigned-only</c>) only see coops assigned to them.
    /// </summary>
    public Guid? FieldOfficerUserId { get; private set; }

    /// <summary>
    /// Survey data (building, equipment, production plan, …) shown on the printed farm data sheet.
    /// </summary>
    public CoopProfile Profile { get; private set; } = new();

    /// <summary>
    /// Lampiran: ids of the attached photos and documents.
    /// </summary>
    public Guid[] Documents { get; private set; } = [];

    public void SetProfile(CoopProfile? profile) => Profile = (profile ?? new CoopProfile()).Normalize();

    public Result SetDocuments(IEnumerable<Guid>? documents) =>
        DocumentList.Apply(documents, value => Documents = value);

    public void AssignFieldOfficer(Guid? userId) =>
        FieldOfficerUserId = userId == Guid.Empty ? null : userId;

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
        Result farmerUsable = EnsureFarmerSubmitted(farmer);
        if (farmerUsable.IsFailure)
        {
            return Result.Failure<Coop>(farmerUsable.Error);
        }

        var coop = new Coop(Guid.CreateVersion7(), Codes.Normalize(code), farmer.Id, farmer.BranchId);

        Result result = coop.Update(name, capacity, houseType, address, latitude, longitude, isActive: false);

        return result.IsSuccess ? coop : Result.Failure<Coop>(result.Error);
    }

    /// <summary>
    /// Submits the coop for approval; its farmer must be approved or waiting for approval itself (M-87).
    /// </summary>
    public Result Submit(Farmer farmer)
    {
        Result result = Registration.Submit(RegistrationStatus, nameof(Coop), Id);
        if (result.IsFailure)
        {
            return result;
        }

        Result farmerUsable = EnsureFarmerSubmitted(farmer);
        if (farmerUsable.IsFailure)
        {
            return farmerUsable;
        }

        RegistrationStatus = RegistrationStatus.PendingApproval;

        return Result.Success();
    }

    /// <summary>
    /// Approves the coop once its farmer is approved (M-87); the coop warehouse is then created from the outbox.
    /// </summary>
    public Result Approve(Farmer farmer)
    {
        Result result = Registration.Decide(RegistrationStatus, nameof(Coop), Id);
        if (result.IsFailure)
        {
            return result;
        }

        if (farmer.RegistrationStatus != RegistrationStatus.Approved)
        {
            return Result.Failure(CoopErrors.FarmerNotApproved);
        }

        RegistrationStatus = RegistrationStatus.Approved;
        IsActive = true;

        Raise(new CoopApprovedDomainEvent(Id));

        return Result.Success();
    }

    public Result Reject()
    {
        Result result = Registration.Decide(RegistrationStatus, nameof(Coop), Id);
        if (result.IsSuccess)
        {
            RegistrationStatus = RegistrationStatus.Rejected;
        }

        return result;
    }

    /// <summary>
    /// The requester cancelled the request: the coop can be edited and submitted again.
    /// </summary>
    public Result ReturnToDraft()
    {
        Result result = Registration.Decide(RegistrationStatus, nameof(Coop), Id);
        if (result.IsSuccess)
        {
            RegistrationStatus = RegistrationStatus.Draft;
        }

        return result;
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
        Result editable = Registration.Edit(RegistrationStatus, nameof(Coop), Id);
        if (editable.IsFailure)
        {
            return editable;
        }

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
        IsActive = isActive && RegistrationStatus == RegistrationStatus.Approved;

        return Result.Success();
    }

    private static Result EnsureFarmerSubmitted(Farmer farmer) => farmer.RegistrationStatus switch
    {
        RegistrationStatus.Approved when !farmer.IsActive => Result.Failure(FarmerErrors.Inactive(farmer.Id)),
        RegistrationStatus.Approved or RegistrationStatus.PendingApproval => Result.Success(),
        _ => Result.Failure(CoopErrors.FarmerNotSubmitted)
    };
}
