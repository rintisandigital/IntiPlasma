using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Inventory;
using Application.Inventory.GoodsReceipts;
using Application.Inventory.StockReturns;
using Application.Inventory.StockTransfers;
using Domain.MasterData.Items;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Inventory;

internal sealed class InventoryEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        MapGoodsReceipts(app.MapGroup("inventory/goods-receipts").WithTags(Tags.GoodsReceipts));
        MapStockTransfers(app.MapGroup("inventory/stock-transfers").WithTags(Tags.StockTransfers));
        MapStock(app.MapGroup("inventory").WithTags(Tags.Stock));
        MapStockReturns(app.MapGroup("inventory").WithTags(Tags.StockReturns));
    }

    private static void MapStockReturns(RouteGroupBuilder group)
    {
        group.MapGet("stock-returns", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            Guid? warehouseId,
            Guid? cycleId,
            DateOnly? from,
            DateOnly? to,
            IQueryHandler<GetStockReturnsQuery, PagedList<InventoryDocumentResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetStockReturnsQuery(
                new PageRequest(page, pageSize, search), branchId, warehouseId, cycleId, from, to);

            Result<PagedList<InventoryDocumentResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.InventoryRead);

        group.MapGet("stock-returns/{stockReturnId:guid}", async (
            Guid stockReturnId,
            IQueryHandler<GetStockReturnByIdQuery, InventoryDocumentResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<InventoryDocumentResponse> result = await handler.Handle(
                new GetStockReturnByIdQuery(stockReturnId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.InventoryRead);

        group.MapPost("stock-returns", async (
            CreateStockReturnCommand command,
            ICommandHandler<CreateStockReturnCommand, CreateStockReturnResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CreateStockReturnResponse> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.InventoryReturn)
        .WithIdempotency();

        group.MapPost("feed-mutations", async (
            CreateFeedMutationCommand command,
            ICommandHandler<CreateFeedMutationCommand, CreateFeedMutationResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CreateFeedMutationResponse> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.InventoryReturn)
        .WithIdempotency();
    }

    private static void MapGoodsReceipts(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            Guid? purchaseOrderId,
            Guid? warehouseId,
            DateOnly? from,
            DateOnly? to,
            IQueryHandler<GetGoodsReceiptsQuery, PagedList<InventoryDocumentResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetGoodsReceiptsQuery(
                new PageRequest(page, pageSize, search), branchId, purchaseOrderId, warehouseId, from, to);

            Result<PagedList<InventoryDocumentResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.InventoryRead);

        group.MapGet("{goodsReceiptId:guid}", async (
            Guid goodsReceiptId,
            IQueryHandler<GetGoodsReceiptByIdQuery, InventoryDocumentResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<InventoryDocumentResponse> result = await handler.Handle(
                new GetGoodsReceiptByIdQuery(goodsReceiptId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.InventoryRead);

        group.MapPost("", async (
            CreateGoodsReceiptCommand command,
            ICommandHandler<CreateGoodsReceiptCommand, CreateGoodsReceiptResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CreateGoodsReceiptResponse> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.InventoryReceive)
        .WithIdempotency();
    }

    private static void MapStockTransfers(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            Guid? warehouseId,
            Guid? cycleId,
            DateOnly? from,
            DateOnly? to,
            IQueryHandler<GetStockTransfersQuery, PagedList<InventoryDocumentResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetStockTransfersQuery(
                new PageRequest(page, pageSize, search), branchId, warehouseId, cycleId, from, to);

            Result<PagedList<InventoryDocumentResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.InventoryRead);

        group.MapGet("{stockTransferId:guid}", async (
            Guid stockTransferId,
            IQueryHandler<GetStockTransferByIdQuery, InventoryDocumentResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<InventoryDocumentResponse> result = await handler.Handle(
                new GetStockTransferByIdQuery(stockTransferId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.InventoryRead);

        group.MapPost("", async (
            CreateStockTransferCommand command,
            ICommandHandler<CreateStockTransferCommand, CreateStockTransferResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CreateStockTransferResponse> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.InventoryTransfer)
        .WithIdempotency();
    }

    private static void MapStock(RouteGroupBuilder group)
    {
        group.MapGet("stock-balances", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            Guid? warehouseId,
            Guid? itemId,
            ItemCategory? category,
            bool? includeEmpty,
            IQueryHandler<GetStockBalancesQuery, PagedList<StockBalanceResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetStockBalancesQuery(
                new PageRequest(page, pageSize, search), branchId, warehouseId, itemId, category, includeEmpty ?? false);

            Result<PagedList<StockBalanceResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.InventoryRead);

        group.MapGet("stock-card", async (
            Guid warehouseId,
            Guid itemId,
            DateOnly from,
            DateOnly to,
            IQueryHandler<GetStockCardQuery, StockCardResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<StockCardResponse> result = await handler.Handle(
                new GetStockCardQuery(warehouseId, itemId, from, to), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.InventoryRead);
    }
}
