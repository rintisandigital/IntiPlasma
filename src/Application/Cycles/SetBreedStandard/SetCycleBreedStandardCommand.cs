using Application.Abstractions.Messaging;

namespace Application.Cycles.SetBreedStandard;

/// <summary>
/// Changes the breed standard of a cycle (PLAN-MOBILE M-71) until it is settled; null = use the default standard.
/// A closed cycle gets the new standard frozen in place of the old one.
/// </summary>
public sealed record SetCycleBreedStandardCommand(Guid CycleId, Guid? BreedStandardId) : ICommand;
