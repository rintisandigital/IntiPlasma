using Application.Abstractions.Messaging;
using Application.Roles.Update;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Roles;

internal sealed class Update : IEndpoint
{
    public sealed record Request(string Name, string? Description, IReadOnlyList<string> Permissions);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("roles/{roleId:guid}", async (
            Guid roleId,
            Request request,
            ICommandHandler<UpdateRoleCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateRoleCommand(roleId, request.Name, request.Description, request.Permissions);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.RolesManage)
        .WithTags(Tags.Roles);
    }
}
