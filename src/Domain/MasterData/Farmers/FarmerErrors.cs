using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Farmers;

public static class FarmerErrors
{
    public static Error NotFound(Guid farmerId) => Error.NotFound(
        "Farmers.NotFound",
        $"The farmer with the Id = '{farmerId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("Farmer", code);

    public static Error Inactive(Guid farmerId) => CommonErrors.Inactive("Farmer", farmerId);

    public static Error PendingApproval(Guid farmerId) => CommonErrors.PendingApproval("Farmer", farmerId);

    public static Error NotSubmittable(Guid farmerId) => CommonErrors.NotSubmittable("Farmer", farmerId);

    public static Error NotPendingApproval(Guid farmerId) => CommonErrors.NotPendingApproval("Farmer", farmerId);

    public static readonly Error ChangePending = Error.Problem(
        "Farmers.ChangePending",
        "A change of the NIK or bank account is already waiting for approval; cancel it before proposing another");

    public static readonly Error PlasmaRequiresNik = Error.Problem(
        "Farmers.PlasmaRequiresNik",
        "A plasma farmer must have a NIK");
}
