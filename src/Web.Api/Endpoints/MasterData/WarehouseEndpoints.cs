using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Warehouses;
using Application.Warehouses.Create;
using Application.Warehouses.Get;
using Application.Warehouses.GetById;
using Application.Warehouses.Update;
using Domain.MasterData.Warehouses;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MasterData;

internal sealed class WarehouseEndpoints : IEndpoint
{
    public sealed record UpdateRequest(string Name, string? Address, bool IsActive);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("warehouses").WithTags(Tags.Warehouses);

        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            WarehouseType? type,
            IQueryHandler<GetWarehousesQuery, PagedList<WarehouseResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PagedList<WarehouseResponse>> result = await handler.Handle(
                new GetWarehousesQuery(new PageRequest(page, pageSize, search), branchId, type), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.WarehousesRead);

        group.MapGet("{warehouseId:guid}", async (
            Guid warehouseId,
            IQueryHandler<GetWarehouseByIdQuery, WarehouseResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<WarehouseResponse> result = await handler.Handle(new GetWarehouseByIdQuery(warehouseId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.WarehousesRead);

        group.MapPost("", async (
            CreateWarehouseCommand command,
            ICommandHandler<CreateWarehouseCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.WarehousesManage)
        .WithIdempotency();

        group.MapPut("{warehouseId:guid}", async (
            Guid warehouseId,
            UpdateRequest request,
            ICommandHandler<UpdateWarehouseCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateWarehouseCommand(warehouseId, request.Name, request.Address, request.IsActive);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.WarehousesManage);
    }
}
