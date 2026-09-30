using Domain.Roles;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.Roles;

internal sealed class GetPermissions : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("permissions", () => Results.Ok(Permissions.All))
            .HasPermission(Permissions.RolesRead)
            .WithTags(Tags.Roles);
    }
}
