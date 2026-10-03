using Application.Abstractions.Auditing;
using Application.Abstractions.Messaging;

namespace Application.Branches.Update;

public sealed record UpdateBranchCommand(Guid BranchId, string Name, string? Address, string? Phone, bool IsActive)
    : ICommand, IAuditedCommand
{
    string IAuditedCommand.AuditEntityType => "Branch";
}
