using Application.Abstractions.Messaging;

namespace Application.Cycles.GetById;

public sealed record GetCycleByIdQuery(Guid CycleId) : IQuery<CycleResponse>;
