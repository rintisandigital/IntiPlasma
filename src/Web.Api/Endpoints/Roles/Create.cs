using Application.Abstractions.Messaging;
using Application.Roles.Create;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Roles;

internal sealed class Create : IEndpoint
{
    public sealed record Request(string Name, string? Description, IReadOnlyList<string> Permissions);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("roles", async (
            Request request,
            ICommandHandler<CreateRoleCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateRoleCommand(request.Name, request.Description, request.Permissions);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.RolesManage)
        .WithIdempotency()
        .WithTags(Tags.Roles);
    }
}
