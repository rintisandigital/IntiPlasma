using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.Partnership.Contracts;
using SharedKernel;

namespace Application.Contracts.GetById;

internal sealed class GetContractByIdQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetContractByIdQuery, ContractResponse>
{
    public async Task<Result<ContractResponse>> Handle(GetContractByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            {ContractResponse.Select}
            WHERE c.id = @ContractId;

            SELECT p.item_id AS ItemId, i.code AS ItemCode, i.name AS ItemName, u.code AS UomCode, p.price AS Price
            FROM partnership.contract_input_prices p
            JOIN master.items i ON i.id = p.item_id
            JOIN master.uoms u ON u.id = i.base_uom_id
            WHERE p.contract_id = @ContractId
            ORDER BY i.category, i.code;

            SELECT p.min_weight_kg AS MinWeightKg, p.max_weight_kg AS MaxWeightKg, p.price_per_kg AS PricePerKg
            FROM partnership.contract_live_bird_prices p
            WHERE p.contract_id = @ContractId
            ORDER BY p.min_weight_kg;

            SELECT i.name AS Name, i.kind AS Kind, i.metric AS Metric, i.range_from AS RangeFrom, i.range_to AS RangeTo,
                   i.amount AS Amount, i.basis AS Basis
            FROM partnership.contract_incentives i
            WHERE i.contract_id = @ContractId
            ORDER BY i.line_number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.ContractId }, cancellationToken: cancellationToken));

        ContractResponse? contract = await multi.ReadSingleOrDefaultAsync<ContractResponse>();

        if (contract is null)
        {
            return Result.Failure<ContractResponse>(ContractErrors.NotFound(query.ContractId));
        }

        Result access = await branchAccess.EnsureAccessAsync(contract.BranchId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<ContractResponse>(access.Error);
        }

        return contract with
        {
            InputPrices = [.. await multi.ReadAsync<ContractResponse.InputPriceResponse>()],
            LiveBirdPrices = [.. await multi.ReadAsync<ContractResponse.LiveBirdPriceResponse>()],
            Incentives = [.. await multi.ReadAsync<ContractResponse.IncentiveResponse>()]
        };
    }
}
