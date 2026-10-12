using SharedKernel;

namespace Domain.MasterData.Coops;

/// <summary>
/// Raised before the approval module (M6) when a coop was created; no longer raised, kept so that messages already in
/// the outbox can still be read. New coops get their warehouse from <see cref="CoopApprovedDomainEvent"/>.
/// </summary>
public sealed record CoopCreatedDomainEvent(Guid CoopId) : DomainEvent;
