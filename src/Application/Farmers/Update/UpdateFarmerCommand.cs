using Application.Abstractions.Messaging;
using Application.Common;

namespace Application.Farmers.Update;

/// <remarks>
/// The type and branch cannot change: running cycles, contracts and settlements depend on them.
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
    IReadOnlyList<Guid>? Documents = null) : ICommand;
