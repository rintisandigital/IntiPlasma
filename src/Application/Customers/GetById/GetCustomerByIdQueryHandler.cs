using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.MasterData.Customers;
using SharedKernel;

namespace Application.Customers.GetById;

internal sealed class GetCustomerByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetCustomerByIdQuery, CustomerResponse>
{
    public async Task<Result<CustomerResponse>> Handle(GetCustomerByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        CustomerQueries.Row? row = await connection.QuerySingleOrDefaultAsync<CustomerQueries.Row>(new CommandDefinition(
            $"{CustomerQueries.Select} WHERE c.id = @CustomerId",
            new { query.CustomerId },
            cancellationToken: cancellationToken));

        return row?.ToResponse() ?? Result.Failure<CustomerResponse>(CustomerErrors.NotFound(query.CustomerId));
    }
}
