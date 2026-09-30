using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.MasterData.Items;
using SharedKernel;

namespace Application.Items.GetById;

internal sealed class GetItemByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetItemByIdQuery, ItemResponse>
{
    public async Task<Result<ItemResponse>> Handle(GetItemByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            """
            SELECT i.id AS Id, i.code AS Code, i.name AS Name, i.category AS Category,
                   i.base_uom_id AS BaseUomId, u.code AS BaseUomCode, i.tax_code_id AS TaxCodeId, t.code AS TaxCode,
                   i.is_active AS IsActive
            FROM master.items i
            JOIN master.uoms u ON u.id = i.base_uom_id
            LEFT JOIN master.tax_codes t ON t.id = i.tax_code_id
            WHERE i.id = @ItemId;

            SELECT c.uom_id AS UomId, u.code AS UomCode, c.factor AS Factor
            FROM master.item_uom_conversions c
            JOIN master.uoms u ON u.id = c.uom_id
            WHERE c.item_id = @ItemId
            ORDER BY u.code;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.ItemId }, cancellationToken: cancellationToken));

        ItemResponse? item = await multi.ReadSingleOrDefaultAsync<ItemResponse>();

        if (item is null)
        {
            return Result.Failure<ItemResponse>(ItemErrors.NotFound(query.ItemId));
        }

        return item with { Conversions = [.. await multi.ReadAsync<ItemUomConversionResponse>()] };
    }
}
