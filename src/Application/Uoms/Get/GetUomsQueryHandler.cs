using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using SharedKernel;

namespace Application.Uoms.Get;

internal sealed class GetUomsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetUomsQuery, IReadOnlyList<UomResponse>>
{
    public async Task<Result<IReadOnlyList<UomResponse>>> Handle(GetUomsQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        IEnumerable<UomResponse> uoms = await connection.QueryAsync<UomResponse>(new CommandDefinition(
            "SELECT u.id AS Id, u.code AS Code, u.name AS Name FROM master.uoms u ORDER BY u.code",
            cancellationToken: cancellationToken));

        return uoms.ToList();
    }
}
