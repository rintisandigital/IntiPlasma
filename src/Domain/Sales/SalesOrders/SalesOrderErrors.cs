using System.Globalization;
using SharedKernel;

namespace Domain.Sales.SalesOrders;

public static class SalesOrderErrors
{
    public static Error NotFound(Guid salesOrderId) => Error.NotFound(
        "SalesOrders.NotFound",
        $"The sales order with the Id = '{salesOrderId}' was not found");

    public static Error NotDraft(Guid salesOrderId) => Error.Problem(
        "SalesOrders.NotDraft",
        $"The sales order with the Id = '{salesOrderId}' is no longer a draft");

    public static Error NotDeliverable(Guid salesOrderId) => Error.Problem(
        "SalesOrders.NotDeliverable",
        $"The sales order with the Id = '{salesOrderId}' is not approved or already fully delivered/closed");

    public static Error InvalidTransition(SalesOrderStatus from, SalesOrderStatus to) => Error.Problem(
        "SalesOrders.InvalidTransition",
        $"A sales order cannot move from {from} to {to}");

    public static Error LineNotFound(int lineNumber) => Error.Problem(
        "SalesOrders.LineNotFound",
        $"The sales order has no line {lineNumber}");

    public static Error OverDelivery(int lineNumber, int outstandingBirds) => Error.Problem(
        "SalesOrders.OverDelivery",
        $"Line {lineNumber} can deliver at most the outstanding {outstandingBirds} birds");

    public static Error InvalidDeliveryReversal(int lineNumber) => Error.Problem(
        "SalesOrders.InvalidDeliveryReversal",
        $"Line {lineNumber} has not delivered that many birds");

    public static Error CreditLimitExceeded(Money limit, Money exposure, Money orderAmount) => Error.Problem(
        "SalesOrders.CreditLimitExceeded",
        string.Create(
            CultureInfo.InvariantCulture,
            $"The customer's credit limit of {limit} is exceeded: exposure {exposure} + this order {orderAmount}; approve with a credit override"));

    public static readonly Error NoLines = Error.Problem(
        "SalesOrders.NoLines",
        "A sales order needs at least one line");

    public static readonly Error DuplicateItem = Error.Problem(
        "SalesOrders.DuplicateItem",
        "An item can only appear once on a sales order");

    public static readonly Error InvalidLine = Error.Problem(
        "SalesOrders.InvalidLine",
        "Each line needs a positive number of birds, estimated weight and price per kg");

    public static readonly Error DeliveryBeforeOrder = Error.Problem(
        "SalesOrders.DeliveryBeforeOrder",
        "The delivery date cannot be before the order date");

    public static readonly Error ItemNotSellable = Error.Problem(
        "SalesOrders.ItemNotSellable",
        "Only active live bird items can be sold");
}
