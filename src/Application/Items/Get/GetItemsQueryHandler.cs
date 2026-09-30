using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using SharedKernel;

namespace Application.Items.Get;

internal sealed class GetItemsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetItemsQuery, PagedList<ItemResponse>>
{
    private const string Filter =
        """
        WHERE (@Search IS NULL OR i.code ILIKE @Search OR i.name ILIKE @Search)
          AND (@Category::text IS NULL OR i.category = @Category)
        """;

    public async Task<Result<PagedList<ItemResponse>>> Handle(GetItemsQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        // Conversions are only returned by the detail endpoint.
        return await connection.QueryPagedAsync<ItemResponse>(
            $"SELECT COUNT(*) FROM master.items i {Filter}",
            $"""
            SELECT i.id AS Id, i.code AS Code, i.name AS Name, i.category AS Category,
                   i.base_uom_id AS BaseUomId, u.code AS BaseUomCode, i.tax_code_id AS TaxCodeId, t.code AS TaxCode,
                   i.is_active AS IsActive
            FROM master.items i
            JOIN master.uoms u ON u.id = i.base_uom_id
            LEFT JOIN master.tax_codes t ON t.id = i.tax_code_id
            {Filter}
            ORDER BY i.code
            LIMIT @PageSize OFFSET @Offset
            """,
            query.Paging,
            new { Category = query.Category?.ToString() },
            cancellationToken);
    }
}
