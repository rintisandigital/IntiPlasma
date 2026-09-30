using Application.Abstractions.Messaging;

namespace Application.Uoms.Update;

public sealed record UpdateUomCommand(Guid UomId, string Name) : ICommand;
