using Application.Abstractions.Messaging;

namespace Application.Cycles.Start;

/// <summary>
/// Chick-in: places DOC from the coop warehouse into the coop. The initial population is the number of DOC placed,
/// so the DOC must first be received in (or transferred to) the coop warehouse.
/// </summary>
public sealed record StartCycleCommand(
    Guid CycleId,
    DateOnly ChickInDate,
    IReadOnlyList<ChickInLine> Lines,
    IReadOnlyList<Guid>? Documents = null) : ICommand<int>;

/// <param name="ItemId">A DOC item in the coop warehouse.</param>
/// <param name="Quantity">Number of birds placed (ekor).</param>
public sealed record ChickInLine(Guid ItemId, int Quantity);
