using SharedKernel;

namespace Domain.Roles;

public sealed record RolePermissionsChangedDomainEvent(Guid RoleId) : DomainEvent;
