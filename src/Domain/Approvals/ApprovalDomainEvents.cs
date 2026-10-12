using SharedKernel;

namespace Domain.Approvals;

/// <summary>
/// Kept in the outbox for future notifications (push, email; M-95).
/// </summary>
public sealed record ApprovalRequestSubmittedDomainEvent(Guid RequestId) : DomainEvent;

public sealed record ApprovalLevelApprovedDomainEvent(Guid RequestId, int LevelNo) : DomainEvent;

public sealed record ApprovalRequestApprovedDomainEvent(Guid RequestId) : DomainEvent;

public sealed record ApprovalRequestRejectedDomainEvent(Guid RequestId) : DomainEvent;

public sealed record ApprovalRequestCancelledDomainEvent(Guid RequestId) : DomainEvent;
