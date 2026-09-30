using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.Partnership.Contracts;
using SharedKernel;

namespace Domain.Partnership.Cycles;

/// <summary>
/// Siklus / masa produksi of one coop, from chick-in until harvest and settlement.
/// A coop can only have one open cycle at a time (also enforced by a unique index).
/// </summary>
public sealed class ProductionCycle : AggregateRoot
{
    public static readonly IReadOnlyList<CycleStatus> OpenStatuses =
        [CycleStatus.Planned, CycleStatus.Active, CycleStatus.Harvesting];

    private readonly List<CycleHarvest> _harvests = [];

    private ProductionCycle(
        Guid id,
        string number,
        Coop coop,
        PartnershipContract? contract,
        DateOnly plannedChickInDate,
        int plannedPopulation,
        string? notes)
        : base(id)
    {
        Number = number;
        BranchId = coop.BranchId;
        FarmerId = coop.FarmerId;
        CoopId = coop.Id;
        ContractId = contract?.Id;
        ContractSnapshot = contract?.CreateSnapshot();
        PlannedChickInDate = plannedChickInDate;
        PlannedPopulation = plannedPopulation;
        Notes = notes;
        Status = CycleStatus.Planned;
    }

    private ProductionCycle()
    {
    }

    public string Number { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid FarmerId { get; private set; }
    public Guid CoopId { get; private set; }
    public Guid? ContractId { get; private set; }

    /// <summary>
    /// Terms of the contract at planning time; null for inti farms, which have no contract.
    /// </summary>
    public ContractSnapshot? ContractSnapshot { get; private set; }

    public CycleStatus Status { get; private set; }
    public DateOnly PlannedChickInDate { get; private set; }
    public int PlannedPopulation { get; private set; }
    public DateOnly? ChickInDate { get; private set; }
    public int? InitialPopulation { get; private set; }
    public string? Notes { get; private set; }
    public string? CancellationReason { get; private set; }

    /// <summary>
    /// Running totals from the daily recordings, kept on the cycle so the population invariant
    /// (never below zero) is checked inside one aggregate.
    /// </summary>
    public int TotalMortality { get; private set; }

    public int TotalCulling { get; private set; }
    public int HarvestedBirds { get; private set; }
    public decimal HarvestedWeightKg { get; private set; }
    public DateOnly? ClosedDate { get; private set; }

    /// <summary>
    /// Performance summary frozen when the cycle is closed; the base for the plasma settlement.
    /// </summary>
    public CyclePerformance? ClosingPerformance { get; private set; }

    public IReadOnlyCollection<CycleHarvest> Harvests => [.. _harvests];

    public int CurrentPopulation => (InitialPopulation ?? 0) - TotalMortality - TotalCulling - HarvestedBirds;

    public bool IsRecordable => Status is CycleStatus.Active or CycleStatus.Harvesting;

    public int AgeOn(DateOnly date) => ChickInDate is null ? 0 : date.DayNumber - ChickInDate.Value.DayNumber;

    /// <summary>
    /// Checks that a daily recording or harvest can be entered for the date.
    /// </summary>
    public Result EnsureRecordable(DateOnly date)
    {
        if (!IsRecordable)
        {
            return Result.Failure(CycleErrors.NotRecordable(Id, Status));
        }

        return date < ChickInDate ? Result.Failure(CycleErrors.BeforeChickIn(ChickInDate!.Value)) : Result.Success();
    }

    /// <summary>
    /// Applies the change in mortality and culling of a (new or revised) daily recording.
    /// </summary>
    public Result ApplyDepletion(int mortalityDelta, int cullingDelta)
    {
        if (!IsRecordable)
        {
            return Result.Failure(CycleErrors.NotRecordable(Id, Status));
        }

        int mortality = TotalMortality + mortalityDelta;
        int culling = TotalCulling + cullingDelta;

        if (mortality < 0 || culling < 0 || (InitialPopulation ?? 0) - mortality - culling - HarvestedBirds < 0)
        {
            return Result.Failure(CycleErrors.PopulationExceeded(CurrentPopulation));
        }

        TotalMortality = mortality;
        TotalCulling = culling;

        return Result.Success();
    }

    public Result<CycleHarvest> RecordHarvest(DateOnly date, int birds, decimal weightKg, string? notes)
    {
        Result recordable = EnsureRecordable(date);
        if (recordable.IsFailure)
        {
            return Result.Failure<CycleHarvest>(recordable.Error);
        }

        if (birds <= 0 || weightKg <= 0)
        {
            return Result.Failure<CycleHarvest>(CycleErrors.InvalidHarvest);
        }

        if (birds > CurrentPopulation)
        {
            return Result.Failure<CycleHarvest>(CycleErrors.PopulationExceeded(CurrentPopulation));
        }

        var harvest = new CycleHarvest(Guid.CreateVersion7(), Id, date, AgeOn(date), birds, weightKg, notes);
        _harvests.Add(harvest);

        HarvestedBirds += birds;
        HarvestedWeightKg += weightKg;
        Status = CycleStatus.Harvesting;

        return harvest;
    }

    /// <summary>
    /// Closes a fully harvested cycle. The caller checks that no sapronak is left in the coop warehouse
    /// and calculates the performance from the recordings.
    /// </summary>
    public Result Close(CyclePerformance performance)
    {
        if (Status != CycleStatus.Harvesting)
        {
            return Result.Failure(CycleErrors.InvalidTransition(Status, CycleStatus.Closed));
        }

        if (CurrentPopulation != 0)
        {
            return Result.Failure(CycleErrors.PopulationRemaining(CurrentPopulation));
        }

        ClosedDate = _harvests.Max(h => h.Date);
        ClosingPerformance = performance;
        Status = CycleStatus.Closed;

        Raise(new CycleClosedDomainEvent(Id));

        return Result.Success();
    }

    /// <summary>
    /// Plans a new cycle. The caller must first make sure the coop has no other open cycle.
    /// </summary>
    public static Result<ProductionCycle> Plan(
        string number,
        Coop coop,
        Farmer farmer,
        PartnershipContract? contract,
        DateOnly plannedChickInDate,
        int plannedPopulation,
        string? notes)
    {
        if (!coop.IsActive)
        {
            return Result.Failure<ProductionCycle>(CoopErrors.Inactive(coop.Id));
        }

        if (!farmer.IsActive)
        {
            return Result.Failure<ProductionCycle>(FarmerErrors.Inactive(farmer.Id));
        }

        if (coop.FarmerId != farmer.Id)
        {
            return Result.Failure<ProductionCycle>(CycleErrors.FarmerMismatch);
        }

        if (plannedPopulation <= 0 || plannedPopulation > coop.Capacity)
        {
            return Result.Failure<ProductionCycle>(CycleErrors.InvalidPopulation(coop.Capacity));
        }

        Result contractResult = ValidateContract(farmer, coop, contract, plannedChickInDate);
        if (contractResult.IsFailure)
        {
            return Result.Failure<ProductionCycle>(contractResult.Error);
        }

        var cycle = new ProductionCycle(
            Guid.CreateVersion7(), number, coop, contract, plannedChickInDate, plannedPopulation, notes);

        cycle.Raise(new CyclePlannedDomainEvent(cycle.Id));

        return cycle;
    }

    /// <summary>
    /// Chick-in: DOC placed in the coop, the cycle starts running.
    /// </summary>
    public Result Start(DateOnly chickInDate, int initialPopulation)
    {
        if (Status != CycleStatus.Planned)
        {
            return Result.Failure(CycleErrors.InvalidTransition(Status, CycleStatus.Active));
        }

        if (initialPopulation <= 0)
        {
            return Result.Failure(CycleErrors.InvalidPopulation(null));
        }

        ChickInDate = chickInDate;
        InitialPopulation = initialPopulation;
        Status = CycleStatus.Active;

        Raise(new CycleStartedDomainEvent(Id));

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status != CycleStatus.Planned)
        {
            return Result.Failure(CycleErrors.InvalidTransition(Status, CycleStatus.Cancelled));
        }

        CancellationReason = reason;
        Status = CycleStatus.Cancelled;

        Raise(new CycleCancelledDomainEvent(Id));

        return Result.Success();
    }

    private static Result ValidateContract(Farmer farmer, Coop coop, PartnershipContract? contract, DateOnly date)
    {
        if (contract is null)
        {
            return farmer.Type == FarmerType.Plasma ? Result.Failure(CycleErrors.ContractRequired) : Result.Success();
        }

        if (farmer.Type == FarmerType.Inti)
        {
            return Result.Failure(CycleErrors.IntiCannotHaveContract);
        }

        if (contract.BranchId != coop.BranchId)
        {
            return Result.Failure(ContractErrors.BranchMismatch);
        }

        return contract.IsUsableOn(date) ? Result.Success() : Result.Failure(ContractErrors.NotUsable(contract.Id, date));
    }
}
