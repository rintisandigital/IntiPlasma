using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using SharedKernel;

namespace Application.Customers.Get;

internal sealed class GetCustomersQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetCustomersQuery, PagedList<CustomerResponse>>
{
    private const string Filter = "WHERE (@Search IS NULL OR c.code ILIKE @Search OR c.name ILIKE @Search)";

    public async Task<Result<PagedList<CustomerResponse>>> Handle(GetCustomersQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        PagedList<CustomerQueries.Row> rows = await connection.QueryPagedAsync<CustomerQueries.Row>(
            $"SELECT COUNT(*) FROM master.customers c {Filter}",
            $"{CustomerQueries.Select} {Filter} ORDER BY c.code LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            null,
            cancellationToken);

        return new PagedList<CustomerResponse>(
            [.. rows.Items.Select(r => r.ToResponse())], rows.Page, rows.PageSize, rows.TotalCount);
    }
}
