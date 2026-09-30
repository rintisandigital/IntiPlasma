using Application.Abstractions.Messaging;

namespace Application.Cycles.Cancel;

public sealed record CancelCycleCommand(Guid CycleId, string Reason) : ICommand;
