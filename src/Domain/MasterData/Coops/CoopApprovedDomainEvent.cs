using SharedKernel;

namespace Domain.MasterData.Coops;

/// <summary>
/// The coop was approved (M-87): its coop warehouse (gudang kandang) is created.
/// </summary>
public sealed record CoopApprovedDomainEvent(Guid CoopId) : DomainEvent;
