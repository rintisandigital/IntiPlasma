using Application.Abstractions.Messaging;
using Application.BreedStandards;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MasterData;

/// <summary>
/// Breed standards (PLAN-MOBILE M-69, M-74). Read-only here: the master is maintained in the WebApp.
/// </summary>
internal sealed class BreedStandardEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("breed-standards").WithTags(Tags.BreedStandards);

        group.MapGet("", async (
            bool? activeOnly,
            IQueryHandler<GetBreedStandardsQuery, IReadOnlyList<BreedStandardResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<BreedStandardResponse>> result =
                await handler.Handle(new GetBreedStandardsQuery(activeOnly ?? true), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataRead);

        group.MapGet("{breedStandardId:guid}", async (
            Guid breedStandardId,
            IQueryHandler<GetBreedStandardByIdQuery, BreedStandardDetailResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<BreedStandardDetailResponse> result =
                await handler.Handle(new GetBreedStandardByIdQuery(breedStandardId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataRead);
    }
}
