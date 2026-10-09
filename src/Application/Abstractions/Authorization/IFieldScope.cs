namespace Application.Abstractions.Authorization;

/// <summary>
/// PPL data scope (PLAN-MOBILE M-36 s.d. M-38): a user with <c>partnership:assigned-only</c> (except the system
/// Administrator role) only sees the farmers and coops assigned to them and the cycles, recordings and
/// attachments of those coops. Applied on top of <see cref="IBranchAccess"/>. Data out of scope is reported as
/// not found, so its existence does not leak.
/// </summary>
public interface IFieldScope
{
    Task<FieldScope> GetScopeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The farmer is assigned to the user directly or owns a coop assigned to the user.
    /// </summary>
    Task<bool> CanAccessFarmerAsync(Guid farmerId, CancellationToken cancellationToken = default);

    Task<bool> CanAccessCoopAsync(Guid coopId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The cycle runs in a coop assigned to the user.
    /// </summary>
    Task<bool> CanAccessCycleAsync(Guid cycleId, CancellationToken cancellationToken = default);
}

/// <param name="Restricted">True when the user only sees the data assigned to them.</param>
/// <param name="UserId">The restricted user; <see cref="Guid.Empty"/> when unrestricted.</param>
public sealed record FieldScope(bool Restricted, Guid UserId)
{
    public static readonly FieldScope Unrestricted = new(false, Guid.Empty);

    /// <summary>
    /// The field officer a new farmer or coop is assigned to: a restricted user always assigns to themselves.
    /// </summary>
    public Guid? AssignFieldOfficer(Guid? requested) => Restricted ? UserId : requested;
}

/// <summary>
/// No PPL scope: Web.App (Akses Menu &amp; Akses Cabang only) and tests.
/// </summary>
public sealed class UnrestrictedFieldScope : IFieldScope
{
    public Task<FieldScope> GetScopeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(FieldScope.Unrestricted);

    public Task<bool> CanAccessFarmerAsync(Guid farmerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<bool> CanAccessCoopAsync(Guid coopId, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<bool> CanAccessCycleAsync(Guid cycleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
