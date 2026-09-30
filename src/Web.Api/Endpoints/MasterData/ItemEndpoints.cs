using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Items;
using Application.Items.Create;
using Application.Items.Get;
using Application.Items.GetById;
using Application.Items.Update;
using Domain.MasterData.Items;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MasterData;

internal sealed class ItemEndpoints : IEndpoint
{
    public sealed record UpdateRequest(
        string Name,
        Guid? TaxCodeId,
        bool IsActive,
        IReadOnlyList<ItemUomConversionRequest> Conversions);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("items").WithTags(Tags.Items);

        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            ItemCategory? category,
            IQueryHandler<GetItemsQuery, PagedList<ItemResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PagedList<ItemResponse>> result = await handler.Handle(
                new GetItemsQuery(new PageRequest(page, pageSize, search), category), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataRead);

        group.MapGet("{itemId:guid}", async (
            Guid itemId,
            IQueryHandler<GetItemByIdQuery, ItemResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<ItemResponse> result = await handler.Handle(new GetItemByIdQuery(itemId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataRead);

        group.MapPost("", async (
            CreateItemCommand command,
            ICommandHandler<CreateItemCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataManage)
        .WithIdempotency();

        group.MapPut("{itemId:guid}", async (
            Guid itemId,
            UpdateRequest request,
            ICommandHandler<UpdateItemCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateItemCommand(itemId, request.Name, request.TaxCodeId, request.IsActive, request.Conversions);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.MasterDataManage);
    }
}
