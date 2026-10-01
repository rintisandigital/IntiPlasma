using SharedKernel;

namespace Domain.Inventory.GoodsReceipts;

public static class GoodsReceiptErrors
{
    public static Error NotFound(Guid goodsReceiptId) => Error.NotFound(
        "GoodsReceipts.NotFound",
        $"The goods receipt with the Id = '{goodsReceiptId}' was not found");

    public static Error CoopHasNoOpenCycle(Guid coopId) => Error.Problem(
        "Inventory.CoopHasNoOpenCycle",
        $"The coop '{coopId}' has no planned or running production cycle to receive stock for");

    public static Error LineNotFound(int lineNumber) => Error.Problem(
        "GoodsReceipts.LineNotFound",
        $"The goods receipt has no line {lineNumber}");

    public static Error OverInvoiced(string number, int lineNumber, decimal uninvoiced) => Error.Problem(
        "GoodsReceipts.OverInvoiced",
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"Line {lineNumber} of goods receipt {number} can be invoiced for at most the uninvoiced quantity of {uninvoiced:0.####}"));

    public static readonly Error InvalidWarehouse = Error.Problem(
        "GoodsReceipts.InvalidWarehouse",
        "The warehouse must be active and belong to the branch of the purchase order");

    public static readonly Error BeforeOrderDate = Error.Problem(
        "GoodsReceipts.BeforeOrderDate",
        "Goods cannot be received before the order date");

    public static readonly Error InvalidLines = Error.Problem(
        "GoodsReceipts.InvalidLines",
        "A receipt needs at least one line, each order line at most once, with a positive quantity");

    public static readonly Error OnlyDocToCoop = Error.Problem(
        "GoodsReceipts.OnlyDocToCoop",
        "Only DOC can be delivered directly to a coop warehouse; feed and OVK go to a central warehouse");
}
