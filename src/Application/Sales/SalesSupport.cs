using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Domain.Sales.DeliveryOrders;
using Domain.Sales.SalesInvoices;
using Domain.Sales.SalesOrders;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales;

internal static class SalesSupport
{
    public static readonly SalesInvoiceStatus[] OpenInvoiceStatuses =
        [SalesInvoiceStatus.Draft, SalesInvoiceStatus.Posted, SalesInvoiceStatus.PartiallyPaid];

    /// <summary>
    /// Posted invoices: part of the receivable ledger.
    /// </summary>
    public static readonly SalesInvoiceStatus[] PostedInvoiceStatuses =
        [SalesInvoiceStatus.Posted, SalesInvoiceStatus.PartiallyPaid, SalesInvoiceStatus.Paid];

    /// <summary>
    /// What the customer owes or has on order, company-wide (a customer's credit limit is not per branch):
    /// outstanding posted invoices + draft invoices + delivered but uninvoiced deliveries + the undelivered part of
    /// other approved sales orders (estimated, excluding VAT).
    /// </summary>
    public static async Task<Money> CreditExposureAsync(
        IApplicationDbContext context,
        Guid customerId,
        Guid excludeSalesOrderId,
        CancellationToken cancellationToken)
    {
        List<SalesInvoice> invoices = await context.SalesInvoices.AsNoTracking()
            .Where(i => i.CustomerId == customerId && OpenInvoiceStatuses.Contains(i.Status))
            .ToListAsync(cancellationToken);

        List<DeliveryOrder> uninvoiced = await context.DeliveryOrders.AsNoTracking()
            .Include(d => d.Lines)
            .Where(d => d.CustomerId == customerId && d.Status == DeliveryOrderStatus.Delivered)
            .ToListAsync(cancellationToken);

        List<SalesOrder> openOrders = await context.SalesOrders.AsNoTracking()
            .Include(o => o.Lines)
            .Where(o => o.CustomerId == customerId && o.Id != excludeSalesOrderId &&
                        (o.Status == SalesOrderStatus.Approved || o.Status == SalesOrderStatus.PartiallyDelivered))
            .ToListAsync(cancellationToken);

        Money exposure = invoices.Aggregate(
            Money.Zero, (total, i) => total + (i.Status == SalesInvoiceStatus.Draft ? i.Total : i.Outstanding));

        exposure = uninvoiced.Aggregate(exposure, (total, d) => total + d.Amount);

        return openOrders.Aggregate(exposure, (total, o) => total + o.OutstandingEstimatedAmount);
    }

    /// <summary>
    /// Number of harvests that are not on a delivery order billed by a posted invoice. A cycle can only be closed
    /// when every harvest has been sold.
    /// </summary>
    public static async Task<int> UnsoldHarvestCountAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> harvestIds,
        CancellationToken cancellationToken)
    {
        List<Guid> sold = await context.DeliveryOrders
            .Where(d => d.Status == DeliveryOrderStatus.Invoiced &&
                        context.SalesInvoices.Any(i => i.Id == d.SalesInvoiceId && PostedInvoiceStatuses.Contains(i.Status)))
            .SelectMany(d => d.Lines)
            .Where(l => harvestIds.Contains(l.HarvestId))
            .Select(l => l.HarvestId)
            .ToListAsync(cancellationToken);

        return harvestIds.Count(id => !sold.Contains(id));
    }

    public static async Task<Result<SalesOrder>> LoadOrderAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid salesOrderId,
        CancellationToken cancellationToken)
    {
        SalesOrder? order = await context.SalesOrders
            .Include(o => o.Lines)
            .SingleOrDefaultAsync(o => o.Id == salesOrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<SalesOrder>(SalesOrderErrors.NotFound(salesOrderId));
        }

        Result access = await branchAccess.EnsureAccessAsync(order.BranchId, cancellationToken);

        return access.IsSuccess ? order : Result.Failure<SalesOrder>(access.Error);
    }

    public static async Task<Result<DeliveryOrder>> LoadDeliveryAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid deliveryOrderId,
        CancellationToken cancellationToken)
    {
        DeliveryOrder? delivery = await context.DeliveryOrders
            .Include(d => d.Lines)
            .SingleOrDefaultAsync(d => d.Id == deliveryOrderId, cancellationToken);

        if (delivery is null)
        {
            return Result.Failure<DeliveryOrder>(DeliveryOrderErrors.NotFound(deliveryOrderId));
        }

        Result access = await branchAccess.EnsureAccessAsync(delivery.BranchId, cancellationToken);

        return access.IsSuccess ? delivery : Result.Failure<DeliveryOrder>(access.Error);
    }

    public static async Task<Result<SalesInvoice>> LoadInvoiceAsync(
        IApplicationDbContext context,
        IBranchAccess branchAccess,
        Guid salesInvoiceId,
        CancellationToken cancellationToken)
    {
        SalesInvoice? invoice = await context.SalesInvoices
            .Include(i => i.Lines)
            .SingleOrDefaultAsync(i => i.Id == salesInvoiceId, cancellationToken);

        if (invoice is null)
        {
            return Result.Failure<SalesInvoice>(SalesInvoiceErrors.NotFound(salesInvoiceId));
        }

        Result access = await branchAccess.EnsureAccessAsync(invoice.BranchId, cancellationToken);

        return access.IsSuccess ? invoice : Result.Failure<SalesInvoice>(access.Error);
    }
}
