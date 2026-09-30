using Application.Abstractions.Messaging;
using Application.Users.AssignRoles;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class AssignRoles : IEndpoint
{
    public sealed record Request(IReadOnlyList<Guid> RoleIds);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/{userId:guid}/roles", async (
            Guid userId,
            Request request,
            ICommandHandler<AssignUserRolesCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AssignUserRolesCommand(userId, request.RoleIds);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersManage)
        .WithTags(Tags.Users);
    }
}
