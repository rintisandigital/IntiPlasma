using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.WeightRanges;

public static class WeightRangeErrors
{
    public static Error NotFound(Guid weightRangeId) => Error.NotFound(
        "WeightRanges.NotFound",
        $"The weight range with the Id = '{weightRangeId}' was not found");

    public static Error CodeNotUnique(string code) => CommonErrors.CodeNotUnique("WeightRange", code);

    public static readonly Error InvalidBounds = Error.Problem(
        "WeightRanges.InvalidBounds",
        "A weight range needs a lower and/or upper bound, the lower bound below the upper bound and neither negative");

    public static Error Overlap(string otherCode) => Error.Conflict(
        "WeightRanges.Overlap",
        $"The weight range overlaps the active weight range '{otherCode}'");

    public static readonly Error InUse = Error.Conflict(
        "WeightRanges.InUse",
        "The bounds of a weight range cannot change once live bird stock entries use it");

    public static Error Inactive(string code) => Error.Problem(
        "WeightRanges.Inactive",
        $"The weight range '{code}' is not active");
}
