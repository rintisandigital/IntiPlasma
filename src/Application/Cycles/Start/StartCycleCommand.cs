using Application.Abstractions.Messaging;

namespace Application.Cycles.Start;

/// <summary>
/// Chick-in: records the actual placement date and DOC population. In phase 4 this is triggered
/// by the DOC receipt at the coop instead of being entered manually.
/// </summary>
public sealed record StartCycleCommand(Guid CycleId, DateOnly ChickInDate, int InitialPopulation) : ICommand;
