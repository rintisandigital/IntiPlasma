using Application.Abstractions.Messaging;

namespace Application.Branches.Create;

public sealed record CreateBranchCommand(string Code, string Name, string? Address, string? Phone) : ICommand<Guid>;
