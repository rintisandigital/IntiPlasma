using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.MasterData.Customers;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales;

/// <summary>
/// Credit position of a customer, as the sales order approval checks it: the credit limit and the exposure (open
/// invoices, uninvoiced deliveries and other open orders, less unapplied advances).
/// </summary>
/// <param name="ExcludeSalesOrderId">The order being approved, which is not part of its own exposure.</param>
public sealed record GetCustomerCreditQuery(Guid CustomerId, Guid? ExcludeSalesOrderId) : IQuery<CustomerCreditResponse>;

/// <param name="Available">Credit limit − exposure (negative when the limit is already exceeded).</param>
public sealed record CustomerCreditResponse(Guid CustomerId, decimal CreditLimit, decimal Exposure, decimal Available);

internal sealed class GetCustomerCreditQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetCustomerCreditQuery, CustomerCreditResponse>
{
    public async Task<Result<CustomerCreditResponse>> Handle(GetCustomerCreditQuery query, CancellationToken cancellationToken)
    {
        Customer? customer = await context.Customers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == query.CustomerId, cancellationToken);

        if (customer is null)
        {
            return Result.Failure<CustomerCreditResponse>(CustomerErrors.NotFound(query.CustomerId));
        }

        Money exposure = await SalesSupport.CreditExposureAsync(
            context, customer.Id, query.ExcludeSalesOrderId ?? Guid.Empty, cancellationToken);

        return new CustomerCreditResponse(
            customer.Id,
            customer.CreditLimit.Amount,
            exposure.Amount,
            customer.CreditLimit.Amount - exposure.Amount);
    }
}
