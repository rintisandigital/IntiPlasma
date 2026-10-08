using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.MasterData.Coops;
using SharedKernel;

namespace Application.Coops.GetById;

internal sealed class GetCoopByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetCoopByIdQuery, CoopResponse>
{
    public async Task<Result<CoopResponse>> Handle(GetCoopByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        CoopResponse? coop = await connection.QuerySingleOrDefaultAsync<CoopResponse>(new CommandDefinition(
            $"{CoopResponse.Select} WHERE c.id = @CoopId",
            new { query.CoopId },
            cancellationToken: cancellationToken));

        if (coop is null)
        {
            return Result.Failure<CoopResponse>(CoopErrors.NotFound(query.CoopId));
        }

        Result access = await branchAccess.EnsureAccessAsync(coop.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CoopResponse>(access.Error);
        }

        string? profile = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT c.profile::text FROM master.coops c WHERE c.id = @CoopId",
            new { query.CoopId },
            cancellationToken: cancellationToken));

        return coop with { Profile = CoopProfileJson.Deserialize(profile) };
    }
}
