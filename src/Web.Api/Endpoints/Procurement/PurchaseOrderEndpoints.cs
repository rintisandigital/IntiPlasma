using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Procurement;
using Domain.Procurement.PurchaseOrders;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Procurement;

internal sealed class PurchaseOrderEndpoints : IEndpoint
{
    public sealed record UpdateRequest(
        DateOnly OrderDate,
        DateOnly? ExpectedDate,
        string? Notes,
        IReadOnlyList<PurchaseOrderLineRequest> Lines);

    public sealed record CancelRequest(string Reason);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("purchase-orders").WithTags(Tags.PurchaseOrders);

        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            Guid? vendorId,
            PurchaseOrderStatus? status,
            IQueryHandler<GetPurchaseOrdersQuery, PagedList<PurchaseOrderResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetPurchaseOrdersQuery(new PageRequest(page, pageSize, search), branchId, vendorId, status);

            Result<PagedList<PurchaseOrderResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.PurchasingRead);

        group.MapGet("{purchaseOrderId:guid}", async (
            Guid purchaseOrderId,
            IQueryHandler<GetPurchaseOrderByIdQuery, PurchaseOrderResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PurchaseOrderResponse> result = await handler.Handle(
                new GetPurchaseOrderByIdQuery(purchaseOrderId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.PurchasingRead);

        group.MapPost("", async (
            CreatePurchaseOrderCommand command,
            ICommandHandler<CreatePurchaseOrderCommand, CreatePurchaseOrderResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CreatePurchaseOrderResponse> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.PurchasingManage)
        .WithIdempotency();

        group.MapPut("{purchaseOrderId:guid}", async (
            Guid purchaseOrderId,
            UpdateRequest request,
            ICommandHandler<UpdatePurchaseOrderCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdatePurchaseOrderCommand(
                purchaseOrderId, request.OrderDate, request.ExpectedDate, request.Notes, request.Lines);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.PurchasingManage);

        group.MapPost("{purchaseOrderId:guid}/approve", async (
            Guid purchaseOrderId,
            ICommandHandler<ApprovePurchaseOrderCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new ApprovePurchaseOrderCommand(purchaseOrderId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.PurchasingApprove);

        group.MapPost("{purchaseOrderId:guid}/cancel", async (
            Guid purchaseOrderId,
            CancelRequest request,
            ICommandHandler<CancelPurchaseOrderCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new CancelPurchaseOrderCommand(purchaseOrderId, request.Reason), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.PurchasingManage);

        group.MapPost("{purchaseOrderId:guid}/close", async (
            Guid purchaseOrderId,
            ICommandHandler<ClosePurchaseOrderCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new ClosePurchaseOrderCommand(purchaseOrderId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.PurchasingManage);
    }
}
