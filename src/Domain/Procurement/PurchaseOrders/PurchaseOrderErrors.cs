using System.Globalization;
using SharedKernel;

namespace Domain.Procurement.PurchaseOrders;

public static class PurchaseOrderErrors
{
    public static Error NotFound(Guid purchaseOrderId) => Error.NotFound(
        "PurchaseOrders.NotFound",
        $"The purchase order with the Id = '{purchaseOrderId}' was not found");

    public static Error NotDraft(Guid purchaseOrderId) => Error.Problem(
        "PurchaseOrders.NotDraft",
        $"The purchase order with the Id = '{purchaseOrderId}' is no longer a draft");

    public static Error NotReceivable(Guid purchaseOrderId) => Error.Problem(
        "PurchaseOrders.NotReceivable",
        $"The purchase order with the Id = '{purchaseOrderId}' is not approved or already fully received/closed");

    public static Error InvalidTransition(PurchaseOrderStatus from, PurchaseOrderStatus to) => Error.Problem(
        "PurchaseOrders.InvalidTransition",
        $"A purchase order cannot move from {from} to {to}");

    public static Error LineNotFound(int lineNumber) => Error.Problem(
        "PurchaseOrders.LineNotFound",
        $"The purchase order has no line {lineNumber}");

    public static Error OverReceipt(int lineNumber, decimal outstanding) => Error.Problem(
        "PurchaseOrders.OverReceipt",
        string.Create(CultureInfo.InvariantCulture, $"Line {lineNumber} can receive at most the outstanding quantity of {outstanding:0.####}"));

    public static readonly Error NoLines = Error.Problem(
        "PurchaseOrders.NoLines",
        "A purchase order needs at least one line");

    public static readonly Error DuplicateItem = Error.Problem(
        "PurchaseOrders.DuplicateItem",
        "An item can only appear once per unit on a purchase order");

    public static readonly Error InvalidLine = Error.Problem(
        "PurchaseOrders.InvalidLine",
        "Each line needs a positive quantity and a positive unit price");

    public static readonly Error ExpectedBeforeOrder = Error.Problem(
        "PurchaseOrders.ExpectedBeforeOrder",
        "The expected delivery date cannot be before the order date");

    public static readonly Error ItemNotPurchasable = Error.Problem(
        "PurchaseOrders.ItemNotPurchasable",
        "Only active DOC, feed and OVK items can be purchased");
}
