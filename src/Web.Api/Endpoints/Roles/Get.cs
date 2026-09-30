using Application.Abstractions.Messaging;
using Application.Roles.Get;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Roles;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("roles", async (
            IQueryHandler<GetRolesQuery, IReadOnlyList<RoleResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<RoleResponse>> result = await handler.Handle(new GetRolesQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.RolesRead)
        .WithTags(Tags.Roles);
    }
}
