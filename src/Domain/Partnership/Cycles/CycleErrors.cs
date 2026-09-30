using SharedKernel;

namespace Domain.Partnership.Cycles;

public static class CycleErrors
{
    public static Error NotFound(Guid cycleId) => Error.NotFound(
        "Cycles.NotFound",
        $"The production cycle with the Id = '{cycleId}' was not found");

    public static Error CoopHasOpenCycle(Guid coopId) => Error.Conflict(
        "Cycles.CoopHasOpenCycle",
        $"The coop with the Id = '{coopId}' already has an open production cycle");

    public static Error InvalidPopulation(int? capacity) => Error.Problem(
        "Cycles.InvalidPopulation",
        capacity is null
            ? "The population must be greater than zero"
            : $"The population must be greater than zero and cannot exceed the coop capacity of {capacity}");

    public static Error InvalidTransition(CycleStatus from, CycleStatus to) => Error.Problem(
        "Cycles.InvalidTransition",
        $"A cycle cannot move from {from} to {to}");

    public static readonly Error FarmerMismatch = Error.Problem(
        "Cycles.FarmerMismatch",
        "The coop does not belong to the farmer");

    public static readonly Error ContractRequired = Error.Problem(
        "Cycles.ContractRequired",
        "A plasma cycle needs an active partnership contract");

    public static Error NotRecordable(Guid cycleId, CycleStatus status) => Error.Problem(
        "Cycles.NotRecordable",
        $"The production cycle with the Id = '{cycleId}' is {status}; recordings and harvests need an active or harvesting cycle");

    public static Error BeforeChickIn(DateOnly chickInDate) => Error.Problem(
        "Cycles.BeforeChickIn",
        $"The date cannot be before the chick-in date {chickInDate:yyyy-MM-dd}");

    public static Error PopulationExceeded(int population) => Error.Problem(
        "Cycles.PopulationExceeded",
        $"The number of birds exceeds the current population of {population}");

    public static Error PopulationRemaining(int population) => Error.Problem(
        "Cycles.PopulationRemaining",
        $"The cycle still has {population} birds; harvest or record them before closing");

    public static Error LeftoverStock(string itemCodes) => Error.Problem(
        "Cycles.LeftoverStock",
        $"The coop warehouse still holds {itemCodes}; return the leftover sapronak to the central warehouse before closing");

    public static Error UnsoldHarvest(int harvests) => Error.Problem(
        "Cycles.UnsoldHarvest",
        $"{harvests} harvest(s) of the cycle are not yet on a delivery order with a posted sales invoice");

    public static Error InsufficientDoc(int requested, decimal available) => Error.Problem(
        "Cycles.InsufficientDoc",
        $"Chick-in of {requested} birds needs that much DOC in the coop warehouse, but only {available:0} is there");

    public static readonly Error InvalidHarvest = Error.Problem(
        "Cycles.InvalidHarvest",
        "A harvest needs a positive number of birds and a positive weight");

    public static readonly Error IntiCannotHaveContract = Error.Problem(
        "Cycles.IntiCannotHaveContract",
        "A cycle on an inti farm cannot have a partnership contract");
}
