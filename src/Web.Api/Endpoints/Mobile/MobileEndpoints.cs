using Application.Abstractions.Messaging;
using Application.Mobile;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Mobile;

/// <summary>
/// Endpoints made for the mobile app (PLAN-MOBILE §4.1).
/// </summary>
internal sealed class MobileEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("mobile").WithTags(Tags.Mobile);

        group.MapGet("field-context", async (
            IQueryHandler<GetFieldContextQuery, FieldContextResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<FieldContextResponse> result = await handler.Handle(new GetFieldContextQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ProductionRead);

        group.MapGet("dashboard", async (
            DateOnly? date,
            Guid? branchId,
            IQueryHandler<GetMobileDashboardQuery, MobileDashboardResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<MobileDashboardResponse> result = await handler.Handle(new GetMobileDashboardQuery(date, branchId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ProductionRead);
    }
}
