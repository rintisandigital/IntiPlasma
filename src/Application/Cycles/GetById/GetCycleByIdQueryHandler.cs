using System.Data.Common;
using System.Text.Json;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.Partnership.Cycles;
using SharedKernel;

namespace Application.Cycles.GetById;

internal sealed class GetCycleByIdQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IBranchAccess branchAccess,
    IFieldScope fieldScope)
    : IQueryHandler<GetCycleByIdQuery, CycleResponse>
{
    public async Task<Result<CycleResponse>> Handle(GetCycleByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {CycleResponse.Select}
            WHERE pc.id = @CycleId;

            SELECT pc.contract_snapshot::text FROM partnership.production_cycles pc WHERE pc.id = @CycleId;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.CycleId }, cancellationToken: cancellationToken));

        CycleResponse? cycle = await multi.ReadSingleOrDefaultAsync<CycleResponse>();

        if (cycle is null || !await fieldScope.CanAccessCycleAsync(cycle.Id, cancellationToken))
        {
            return Result.Failure<CycleResponse>(CycleErrors.NotFound(query.CycleId));
        }

        Result access = await branchAccess.EnsureAccessAsync(cycle.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<CycleResponse>(access.Error);
        }

        string? snapshot = await multi.ReadSingleOrDefaultAsync<string?>();

        return cycle with
        {
            ContractSnapshot = snapshot is null ? null : JsonSerializer.Deserialize<JsonElement>(snapshot)
        };
    }
}
