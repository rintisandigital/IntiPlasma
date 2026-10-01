using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Vendors;

public static class VendorErrors
{
    public static Error NotFound(Guid vendorId) => Error.NotFound(
        "Vendors.NotFound",
        $"The vendor with the Id = '{vendorId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("Vendor", code);

    public static readonly Error InvalidPriceTolerance = Error.Problem(
        "Vendors.InvalidPriceTolerance",
        "The price tolerance must be between 0 and 100 percent");
}
