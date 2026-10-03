using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Users.Manage;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// User administration (PLAN-WEBAPP §11.6). ⚠️ <c>PUT users/{id}/branches</c> was replaced by
/// <c>PUT users/{id}/access</c> (branch access profiles).
/// </summary>
internal sealed class UserManagementEndpoints : IEndpoint
{
    public sealed record UpdateRequest(string FirstName, string LastName);

    public sealed record AccessRequest(Guid? MenuAccessProfileId, Guid? BranchAccessProfileId, Guid? DefaultBranchId);

    public sealed record ResetPasswordRequest(string NewPassword);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("users").WithTags(Tags.Users);

        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            bool? isActive,
            Guid? menuAccessProfileId,
            Guid? branchAccessProfileId,
            IQueryHandler<GetUsersQuery, PagedList<UserListItem>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PagedList<UserListItem>> result = await handler.Handle(
                new GetUsersQuery(new PageRequest(page, pageSize, search), isActive, menuAccessProfileId, branchAccessProfileId),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersRead);

        group.MapPut("{userId:guid}", async (
            Guid userId,
            UpdateRequest request,
            ICommandHandler<UpdateUserCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new UpdateUserCommand(userId, request.FirstName, request.LastName), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersManage);

        group.MapPut("{userId:guid}/access", async (
            Guid userId,
            AccessRequest request,
            ICommandHandler<SetUserAccessCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new SetUserAccessCommand(userId, request.MenuAccessProfileId, request.BranchAccessProfileId, request.DefaultBranchId),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersManage);

        group.MapPost("{userId:guid}/deactivate", async (
            Guid userId,
            ICommandHandler<DeactivateUserCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new DeactivateUserCommand(userId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersManage);

        group.MapPost("{userId:guid}/activate", async (
            Guid userId,
            ICommandHandler<ActivateUserCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new ActivateUserCommand(userId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersManage);

        group.MapPost("{userId:guid}/unlock", async (
            Guid userId,
            ICommandHandler<UnlockUserCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new UnlockUserCommand(userId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersManage);

        group.MapPost("{userId:guid}/reset-password", async (
            Guid userId,
            ResetPasswordRequest request,
            ICommandHandler<ResetUserPasswordCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new ResetUserPasswordCommand(userId, request.NewPassword), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersManage);

        group.MapDelete("{userId:guid}", async (
            Guid userId,
            string? reason,
            ICommandHandler<DeleteUserCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new DeleteUserCommand(userId, reason), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersManage);
    }
}
