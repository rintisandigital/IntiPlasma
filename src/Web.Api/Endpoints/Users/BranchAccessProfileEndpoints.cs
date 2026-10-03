using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Access.BranchAccessProfiles;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// Branch access profiles ("Akses Cabang"): the branch scope of users in Web.Api and Web.App. Menu access
/// profiles are Web.App only and have no API (W-24).
/// </summary>
internal sealed class BranchAccessProfileEndpoints : IEndpoint
{
    public sealed record ProfileRequest(string Name, string? Description, bool AllBranches, IReadOnlyList<Guid> BranchIds);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("branch-access-profiles").WithTags(Tags.Users);

        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            IQueryHandler<GetBranchAccessProfilesQuery, PagedList<BranchAccessProfileListItem>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PagedList<BranchAccessProfileListItem>> result = await handler.Handle(
                new GetBranchAccessProfilesQuery(new PageRequest(page, pageSize, search)), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersRead);

        group.MapGet("{profileId:guid}", async (
            Guid profileId,
            IQueryHandler<GetBranchAccessProfileByIdQuery, BranchAccessProfileResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<BranchAccessProfileResponse> result = await handler.Handle(
                new GetBranchAccessProfileByIdQuery(profileId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersRead);

        group.MapPost("", async (
            ProfileRequest request,
            ICommandHandler<CreateBranchAccessProfileCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(
                new CreateBranchAccessProfileCommand(request.Name, request.Description, request.AllBranches, request.BranchIds ?? []),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersManage)
        .WithIdempotency();

        group.MapPut("{profileId:guid}", async (
            Guid profileId,
            ProfileRequest request,
            ICommandHandler<UpdateBranchAccessProfileCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new UpdateBranchAccessProfileCommand(profileId, request.Name, request.Description, request.AllBranches, request.BranchIds ?? []),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersManage);

        group.MapDelete("{profileId:guid}", async (
            Guid profileId,
            ICommandHandler<DeleteBranchAccessProfileCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new DeleteBranchAccessProfileCommand(profileId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersManage);
    }
}
