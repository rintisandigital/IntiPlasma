using Application.Abstractions.Messaging;
using Application.Users.AssignBranches;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class AssignBranches : IEndpoint
{
    public sealed record Request(IReadOnlyList<Guid> BranchIds);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/{userId:guid}/branches", async (
            Guid userId,
            Request request,
            ICommandHandler<AssignUserBranchesCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AssignUserBranchesCommand(userId, request.BranchIds);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersManage)
        .WithTags(Tags.Users);
    }
}
