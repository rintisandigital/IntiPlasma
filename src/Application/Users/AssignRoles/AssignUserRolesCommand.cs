using Application.Abstractions.Messaging;

namespace Application.Users.AssignRoles;

public sealed record AssignUserRolesCommand(Guid UserId, IReadOnlyList<Guid> RoleIds) : ICommand;
