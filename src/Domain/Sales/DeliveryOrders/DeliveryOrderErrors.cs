using SharedKernel;

namespace Domain.Sales.DeliveryOrders;

public static class DeliveryOrderErrors
{
    public static Error NotFound(Guid deliveryOrderId) => Error.NotFound(
        "DeliveryOrders.NotFound",
        $"The delivery order with the Id = '{deliveryOrderId}' was not found");

    public static Error InvalidTransition(DeliveryOrderStatus from, DeliveryOrderStatus to) => Error.Problem(
        "DeliveryOrders.InvalidTransition",
        $"A delivery order cannot move from {from} to {to}");

    public static Error NotInvoiceable(Guid deliveryOrderId) => Error.Problem(
        "DeliveryOrders.NotInvoiceable",
        $"The delivery order with the Id = '{deliveryOrderId}' is cancelled or already on an invoice");

    public static Error HarvestNotFound(Guid harvestId) => Error.NotFound(
        "DeliveryOrders.HarvestNotFound",
        $"The harvest with the Id = '{harvestId}' was not found");

    public static Error HarvestAlreadyDelivered(Guid harvestId) => Error.Conflict(
        "DeliveryOrders.HarvestAlreadyDelivered",
        $"The harvest with the Id = '{harvestId}' is already on another delivery order");

    public static Error BeforeHarvestDate(DateOnly harvestDate) => Error.Problem(
        "DeliveryOrders.BeforeHarvestDate",
        $"The delivery date cannot be before the harvest date {harvestDate:yyyy-MM-dd}");

    public static readonly Error NoLines = Error.Problem(
        "DeliveryOrders.NoLines",
        "A delivery order needs at least one harvest");

    public static readonly Error DuplicateHarvest = Error.Problem(
        "DeliveryOrders.DuplicateHarvest",
        "A harvest can only appear once on a delivery order");

    public static readonly Error HarvestBranchMismatch = Error.Problem(
        "DeliveryOrders.HarvestBranchMismatch",
        "The harvest must come from a cycle of the sales order's branch");

    public static readonly Error BeforeOrderDate = Error.Problem(
        "DeliveryOrders.BeforeOrderDate",
        "The delivery date cannot be before the sales order date");
}
