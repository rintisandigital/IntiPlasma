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

    public static readonly Error PlasmaRequiresNik = Error.Problem(
        "Farmers.PlasmaRequiresNik",
        "A plasma farmer must have a NIK");
}
