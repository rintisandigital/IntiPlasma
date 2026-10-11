using Application.Abstractions.Messaging;

namespace Application.Cycles.Plan;

/// <param name="ContractId">Required for plasma coops, must be empty for inti coops.</param>
/// <param name="BreedStandardId">Breed standard to compare the performance with; null = the default standard.</param>
public sealed record PlanCycleCommand(
    Guid CoopId,
    Guid? ContractId,
    DateOnly PlannedChickInDate,
    int PlannedPopulation,
    string? Notes,
    Guid? BreedStandardId = null) : ICommand<PlanCycleResponse>;

public sealed record PlanCycleResponse(Guid Id, string Number);
