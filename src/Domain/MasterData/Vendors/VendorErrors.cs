using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Vendors;

public static class VendorErrors
{
    public static Error NotFound(Guid vendorId) => Error.NotFound(
        "Vendors.NotFound",
        $"The vendor with the Id = '{vendorId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("Vendor", code);
}
