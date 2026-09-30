using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.MasterData.Farmers;
using SharedKernel;

namespace Application.Farmers.GetById;

internal sealed class GetFarmerByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetFarmerByIdQuery, FarmerResponse>
{
    public async Task<Result<FarmerResponse>> Handle(GetFarmerByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        FarmerQueries.Row? row = await connection.QuerySingleOrDefaultAsync<FarmerQueries.Row>(new CommandDefinition(
            $"{FarmerQueries.Select} WHERE f.id = @FarmerId",
            new { query.FarmerId },
            cancellationToken: cancellationToken));

        if (row is null)
        {
            return Result.Failure<FarmerResponse>(FarmerErrors.NotFound(query.FarmerId));
        }

        Result access = await branchAccess.EnsureAccessAsync(row.BranchId, cancellationToken);

        return access.IsSuccess ? row.ToResponse() : Result.Failure<FarmerResponse>(access.Error);
    }
}
