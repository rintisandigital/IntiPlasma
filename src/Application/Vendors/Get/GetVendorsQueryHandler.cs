using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using SharedKernel;

namespace Application.Vendors.Get;

internal sealed class GetVendorsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetVendorsQuery, PagedList<VendorResponse>>
{
    private const string Filter = "WHERE (@Search IS NULL OR v.code ILIKE @Search OR v.name ILIKE @Search)";

    public async Task<Result<PagedList<VendorResponse>>> Handle(GetVendorsQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        PagedList<VendorQueries.Row> rows = await connection.QueryPagedAsync<VendorQueries.Row>(
            $"SELECT COUNT(*) FROM master.vendors v {Filter}",
            $"{VendorQueries.Select} {Filter} ORDER BY v.code LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            null,
            cancellationToken);

        return new PagedList<VendorResponse>(
            [.. rows.Items.Select(r => r.ToResponse())], rows.Page, rows.PageSize, rows.TotalCount);
    }
}
