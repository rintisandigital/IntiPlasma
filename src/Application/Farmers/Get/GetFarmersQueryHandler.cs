using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using SharedKernel;

namespace Application.Farmers.Get;

internal sealed class GetFarmersQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IBranchAccess branchAccess,
    IFieldScope fieldScope)
    : IQueryHandler<GetFarmersQuery, PagedList<FarmerResponse>>
{
    private static readonly string Filter =
        $"""
         WHERE (@AllBranches OR f.branch_id = ANY(@BranchIds))
           AND {FieldScopeSql.Farmer("f")}
           AND (@BranchId::uuid IS NULL OR f.branch_id = @BranchId)
           AND (@Type::text IS NULL OR f.type = @Type)
           AND (@FieldOfficerId::uuid IS NULL OR f.field_officer_user_id = @FieldOfficerId
                OR EXISTS (SELECT 1 FROM master.coops fc
                           WHERE fc.farmer_id = f.id AND fc.field_officer_user_id = @FieldOfficerId))
           AND (@Search IS NULL OR f.code ILIKE @Search OR f.name ILIKE @Search OR f.nik ILIKE @Search)
         """;

    public async Task<Result<PagedList<FarmerResponse>>> Handle(GetFarmersQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);
        FieldScope field = await fieldScope.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        PagedList<FarmerQueries.Row> rows = await connection.QueryPagedAsync<FarmerQueries.Row>(
            $"SELECT COUNT(*) FROM master.farmers f {Filter}",
            $"{FarmerQueries.Select} {Filter} ORDER BY f.code LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                FieldRestricted = field.Restricted,
                FieldUserId = field.UserId,
                query.BranchId,
                Type = query.Type?.ToString(),
                query.FieldOfficerId
            },
            cancellationToken);

        return new PagedList<FarmerResponse>(
            [.. rows.Items.Select(r => r.ToResponse())], rows.Page, rows.PageSize, rows.TotalCount);
    }
}
