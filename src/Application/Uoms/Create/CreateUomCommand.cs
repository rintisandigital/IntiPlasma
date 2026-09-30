using Application.Abstractions.Messaging;

namespace Application.Uoms.Create;

public sealed record CreateUomCommand(string Code, string Name) : ICommand<Guid>;
