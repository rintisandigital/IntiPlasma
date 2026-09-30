using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.Uoms;

public static class UomErrors
{
    public static Error NotFound(Guid uomId) => Error.NotFound(
        "Uoms.NotFound",
        $"The unit of measure with the Id = '{uomId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("Uom", code);
}
