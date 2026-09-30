using SharedKernel;

namespace Domain.Inventory.StockTransfers;

public static class StockTransferErrors
{
    public static Error NotFound(Guid stockTransferId) => Error.NotFound(
        "StockTransfers.NotFound",
        $"The stock transfer with the Id = '{stockTransferId}' was not found");

    public static readonly Error SameWarehouse = Error.Problem(
        "StockTransfers.SameWarehouse",
        "The source and destination warehouse must differ");

    public static readonly Error SourceMustBeCentral = Error.Problem(
        "StockTransfers.SourceMustBeCentral",
        "Stock can only be transferred out of a central warehouse (returns from coops come in a later phase)");

    public static readonly Error CrossBranch = Error.Problem(
        "StockTransfers.CrossBranch",
        "Both warehouses must belong to the same branch");

    public static readonly Error InactiveWarehouse = Error.Problem(
        "StockTransfers.InactiveWarehouse",
        "Both warehouses must be active");

    public static readonly Error OnlySapronakToCoop = Error.Problem(
        "StockTransfers.OnlySapronakToCoop",
        "Only DOC, feed and OVK can be transferred to a coop");

    public static readonly Error InvalidLines = Error.Problem(
        "StockTransfers.InvalidLines",
        "A transfer needs at least one line, each item at most once, with a positive quantity");
}
