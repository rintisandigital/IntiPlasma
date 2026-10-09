using Application.Abstractions.Messaging;
using Application.WeightRanges;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MasterData;

/// <summary>
/// Rentang bobot for stok ayam harian (PLAN-MOBILE M-23). Read-only here: the master is maintained in the WebApp.
/// </summary>
internal sealed class WeightRangeEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("weight-ranges", async (
            bool? activeOnly,
            IQueryHandler<GetWeightRangesQuery, IReadOnlyList<WeightRangeResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<WeightRangeResponse>> result =
                await handler.Handle(new GetWeightRangesQuery(activeOnly ?? true), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.WeightRanges)
        .HasPermission(Permissions.MasterDataRead);
    }
}
