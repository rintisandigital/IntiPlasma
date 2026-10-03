using Application.Abstractions.Auditing;
using Application.Abstractions.Messaging;

namespace Application.Roles.Update;

public sealed record UpdateRoleCommand(Guid RoleId, string Name, string? Description, IReadOnlyList<string> Permissions)
    : ICommand, IAuditedCommand
{
    string IAuditedCommand.AuditEntityType => "Role";
}
