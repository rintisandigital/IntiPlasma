using Application.Abstractions.Messaging;

namespace Application.Cycles.Plan;

/// <param name="ContractId">Required for plasma coops, must be empty for inti coops.</param>
public sealed record PlanCycleCommand(
    Guid CoopId,
    Guid? ContractId,
    DateOnly PlannedChickInDate,
    int PlannedPopulation,
    string? Notes) : ICommand<PlanCycleResponse>;

public sealed record PlanCycleResponse(Guid Id, string Number);
