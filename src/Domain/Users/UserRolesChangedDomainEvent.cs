using SharedKernel;

namespace Domain.Users;

public sealed record UserRolesChangedDomainEvent(Guid UserId) : DomainEvent;
