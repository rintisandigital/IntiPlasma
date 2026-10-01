using Application.Abstractions.Messaging;
using Application.Common;
using Domain.MasterData.Farmers;

namespace Application.Farmers.Create;

public sealed record CreateFarmerCommand(
    string Code,
    string Name,
    FarmerType Type,
    Guid BranchId,
    string? Nik,
    TaxIdentityRequest TaxIdentity,
    string? Address,
    string? Phone,
    BankAccountRequest BankAccount,
    IReadOnlyList<Guid>? Documents = null) : ICommand<Guid>;
