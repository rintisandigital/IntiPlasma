using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Branches;
using Application.Branches.Create;
using Application.Branches.Get;
using Application.Branches.GetById;
using Application.Branches.Update;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Branches;

internal sealed class BranchEndpoints : IEndpoint
{
    public sealed record UpdateRequest(string Name, string? Address, string? Phone, bool IsActive);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("branches").WithTags(Tags.Branches);

        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            IQueryHandler<GetBranchesQuery, PagedList<BranchResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PagedList<BranchResponse>> result = await handler.Handle(
                new GetBranchesQuery(new PageRequest(page, pageSize, search)), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.BranchesRead);

        group.MapGet("{branchId:guid}", async (
            Guid branchId,
            IQueryHandler<GetBranchByIdQuery, BranchResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<BranchResponse> result = await handler.Handle(new GetBranchByIdQuery(branchId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.BranchesRead);

        group.MapPost("", async (
            CreateBranchCommand command,
            ICommandHandler<CreateBranchCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.BranchesManage)
        .WithIdempotency();

        group.MapPut("{branchId:guid}", async (
            Guid branchId,
            UpdateRequest request,
            ICommandHandler<UpdateBranchCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateBranchCommand(branchId, request.Name, request.Address, request.Phone, request.IsActive);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.BranchesManage);
    }
}
