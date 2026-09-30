using Domain.Common;
using SharedKernel;

namespace Domain.Finance.CostCenters;

public static class CostCenterErrors
{
    public static Error NotFound(Guid costCenterId) => Error.NotFound(
        "CostCenters.NotFound",
        $"The cost center with the Id = '{costCenterId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("CostCenter", code);
}
