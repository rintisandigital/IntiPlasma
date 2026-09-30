using SharedKernel;

namespace Domain.Inventory.StockReturns;

public static class StockReturnErrors
{
    public static Error NotFound(Guid stockReturnId) => Error.NotFound(
        "StockReturns.NotFound",
        $"The stock return with the Id = '{stockReturnId}' was not found");

    public static readonly Error SourceMustBeCoop = Error.Problem(
        "StockReturns.SourceMustBeCoop",
        "A return must come from a coop warehouse");

    public static readonly Error DestinationMustBeCentral = Error.Problem(
        "StockReturns.DestinationMustBeCentral",
        "Returned sapronak must go to a central warehouse; feed moves between coops only via a central warehouse");

    public static readonly Error MutationTargetMustBeCoop = Error.Problem(
        "StockReturns.MutationTargetMustBeCoop",
        "A feed mutation must end in another coop warehouse");

    public static readonly Error OnlyFeedAndOvk = Error.Problem(
        "StockReturns.OnlyFeedAndOvk",
        "Only feed and OVK can be returned");
}
