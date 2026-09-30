using Application.Abstractions.Messaging;

namespace Application.Coops.GetById;

public sealed record GetCoopByIdQuery(Guid CoopId) : IQuery<CoopResponse>;
