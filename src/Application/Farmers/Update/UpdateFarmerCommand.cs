using Application.Abstractions.Messaging;
using Application.Common;

namespace Application.Farmers.Update;

/// <remarks>
/// The type and branch cannot change: running cycles, contracts and settlements depend on them.
/// <see cref="FieldOfficerUserId"/> replaces the assignment (null = none), except for a user limited to assigned
/// data, who cannot reassign.
/// </remarks>
public sealed record UpdateFarmerCommand(
    Guid FarmerId,
    string Name,
    string? Nik,
    TaxIdentityRequest TaxIdentity,
    string? Address,
    string? Phone,
    BankAccountRequest BankAccount,
    bool IsActive,
    IReadOnlyList<Guid>? Documents = null,
    Guid? FieldOfficerUserId = null) : ICommand;
