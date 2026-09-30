using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.MasterData.Vendors;
using SharedKernel;

namespace Application.Vendors.GetById;

internal sealed class GetVendorByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetVendorByIdQuery, VendorResponse>
{
    public async Task<Result<VendorResponse>> Handle(GetVendorByIdQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        VendorQueries.Row? row = await connection.QuerySingleOrDefaultAsync<VendorQueries.Row>(new CommandDefinition(
            $"{VendorQueries.Select} WHERE v.id = @VendorId",
            new { query.VendorId },
            cancellationToken: cancellationToken));

        return row?.ToResponse() ?? Result.Failure<VendorResponse>(VendorErrors.NotFound(query.VendorId));
    }
}
