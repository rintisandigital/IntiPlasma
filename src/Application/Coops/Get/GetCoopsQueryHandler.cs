using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using SharedKernel;

namespace Application.Coops.Get;

internal sealed class GetCoopsQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IBranchAccess branchAccess,
    IFieldScope fieldScope)
    : IQueryHandler<GetCoopsQuery, PagedList<CoopResponse>>
{
    private static readonly string Filter =
        $"""
         WHERE (@AllBranches OR c.branch_id = ANY(@BranchIds))
           AND {FieldScopeSql.Coop("c")}
           AND (@BranchId::uuid IS NULL OR c.branch_id = @BranchId)
           AND (@FarmerId::uuid IS NULL OR c.farmer_id = @FarmerId)
           AND (@FieldOfficerId::uuid IS NULL OR c.field_officer_user_id = @FieldOfficerId)
           AND (@Search IS NULL OR c.code ILIKE @Search OR c.name ILIKE @Search)
         """;

    public async Task<Result<PagedList<CoopResponse>>> Handle(GetCoopsQuery query, CancellationToken cancellationToken)
    {
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);
        FieldScope field = await fieldScope.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        return await connection.QueryPagedAsync<CoopResponse>(
            $"SELECT COUNT(*) FROM master.coops c {Filter}",
            $"{CoopResponse.Select} {Filter} ORDER BY c.code LIMIT @PageSize OFFSET @Offset",
            query.Paging,
            new
            {
                scope.AllBranches,
                scope.BranchIds,
                FieldRestricted = field.Restricted,
                FieldUserId = field.UserId,
                query.BranchId,
                query.FarmerId,
                query.FieldOfficerId
            },
            cancellationToken);
    }
}
