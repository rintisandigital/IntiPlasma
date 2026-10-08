using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Coops;
using Application.Coops.Create;
using Application.Coops.Get;
using Application.Coops.GetById;
using Application.Coops.Update;
using Domain.MasterData.Coops;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Farmers;

internal sealed class CoopEndpoints : IEndpoint
{
    public sealed record UpdateRequest(
        string Name,
        int Capacity,
        HouseType HouseType,
        string? Address,
        decimal? Latitude,
        decimal? Longitude,
        bool IsActive,
        IReadOnlyList<Guid>? Documents = null,
        CoopProfile? Profile = null);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("coops").WithTags(Tags.Coops);

        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            Guid? farmerId,
            IQueryHandler<GetCoopsQuery, PagedList<CoopResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PagedList<CoopResponse>> result = await handler.Handle(
                new GetCoopsQuery(new PageRequest(page, pageSize, search), branchId, farmerId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FarmersRead);

        group.MapGet("{coopId:guid}", async (
            Guid coopId,
            IQueryHandler<GetCoopByIdQuery, CoopResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CoopResponse> result = await handler.Handle(new GetCoopByIdQuery(coopId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FarmersRead);

        group.MapPost("", async (
            CreateCoopCommand command,
            ICommandHandler<CreateCoopCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.FarmersManage)
        .WithIdempotency();

        group.MapPut("{coopId:guid}", async (
            Guid coopId,
            UpdateRequest request,
            ICommandHandler<UpdateCoopCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateCoopCommand(
                coopId,
                request.Name,
                request.Capacity,
                request.HouseType,
                request.Address,
                request.Latitude,
                request.Longitude,
                request.IsActive,
                request.Documents,
                request.Profile);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.FarmersManage);
    }
}
