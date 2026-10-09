using Application.Abstractions.Messaging;
using Application.Common;
using Domain.MasterData.Farmers;

namespace Application.Farmers.Create;

/// <param name="FieldOfficerUserId">
/// PPL responsible for the farmer; ignored for a user limited to assigned data, who is assigned automatically.
/// </param>
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
    IReadOnlyList<Guid>? Documents = null,
    Guid? FieldOfficerUserId = null) : ICommand<Guid>;
