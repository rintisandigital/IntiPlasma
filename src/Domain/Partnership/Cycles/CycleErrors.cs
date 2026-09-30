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

    public static readonly Error IntiCannotHaveContract = Error.Problem(
        "Cycles.IntiCannotHaveContract",
        "A cycle on an inti farm cannot have a partnership contract");
}
