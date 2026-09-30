using Application.Abstractions.Messaging;

namespace Application.Uoms.Get;

public sealed record GetUomsQuery : IQuery<IReadOnlyList<UomResponse>>;

public sealed record UomResponse(Guid Id, string Code, string Name);
