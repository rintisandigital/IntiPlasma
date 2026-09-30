using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Sales;
using Domain.Roles;
using Domain.Sales.DeliveryOrders;
using Domain.Sales.SalesInvoices;
using Domain.Sales.SalesOrders;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Sales;

internal sealed class SalesEndpoints : IEndpoint
{
    public sealed record UpdateSalesOrderRequest(
        DateOnly OrderDate,
        DateOnly? DeliveryDate,
        string? Notes,
        IReadOnlyList<SalesOrderLineRequest> Lines);

    public sealed record ReasonRequest(string Reason);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        MapSalesOrders(app.MapGroup("sales/orders").WithTags(Tags.SalesOrders));
        MapDeliveryOrders(app.MapGroup("sales").WithTags(Tags.DeliveryOrders));
        MapInvoices(app.MapGroup("sales/invoices").WithTags(Tags.SalesInvoices));
    }

    private static void MapSalesOrders(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            Guid? customerId,
            SalesOrderStatus? status,
            DateOnly? from,
            DateOnly? to,
            IQueryHandler<GetSalesOrdersQuery, PagedList<SalesOrderResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetSalesOrdersQuery(new PageRequest(page, pageSize, search), branchId, customerId, status, from, to);

            Result<PagedList<SalesOrderResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesRead);

        group.MapGet("{salesOrderId:guid}", async (
            Guid salesOrderId,
            IQueryHandler<GetSalesOrderByIdQuery, SalesOrderResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<SalesOrderResponse> result = await handler.Handle(new GetSalesOrderByIdQuery(salesOrderId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesRead);

        group.MapPost("", async (
            CreateSalesOrderCommand command,
            ICommandHandler<CreateSalesOrderCommand, CreateSalesOrderResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CreateSalesOrderResponse> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesManage)
        .WithIdempotency();

        group.MapPut("{salesOrderId:guid}", async (
            Guid salesOrderId,
            UpdateSalesOrderRequest request,
            ICommandHandler<UpdateSalesOrderCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateSalesOrderCommand(
                salesOrderId, request.OrderDate, request.DeliveryDate, request.Notes, request.Lines);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesManage);

        group.MapPost("{salesOrderId:guid}/approve", async (
            Guid salesOrderId,
            ICommandHandler<ApproveSalesOrderCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new ApproveSalesOrderCommand(salesOrderId, null), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesApprove);

        group.MapPost("{salesOrderId:guid}/approve-over-limit", async (
            Guid salesOrderId,
            ReasonRequest request,
            ICommandHandler<ApproveSalesOrderCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new ApproveSalesOrderCommand(salesOrderId, request.Reason), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesCreditOverride);

        group.MapPost("{salesOrderId:guid}/cancel", async (
            Guid salesOrderId,
            ReasonRequest request,
            ICommandHandler<CancelSalesOrderCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new CancelSalesOrderCommand(salesOrderId, request.Reason), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesManage);

        group.MapPost("{salesOrderId:guid}/close", async (
            Guid salesOrderId,
            ICommandHandler<CloseSalesOrderCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new CloseSalesOrderCommand(salesOrderId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesManage);
    }

    private static void MapDeliveryOrders(RouteGroupBuilder group)
    {
        group.MapGet("undelivered-harvests", async (
            Guid? branchId,
            Guid? cycleId,
            IQueryHandler<GetUndeliveredHarvestsQuery, IReadOnlyList<UndeliveredHarvestResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<UndeliveredHarvestResponse>> result = await handler.Handle(
                new GetUndeliveredHarvestsQuery(branchId, cycleId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesRead);

        group.MapGet("delivery-orders", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            Guid? customerId,
            Guid? salesOrderId,
            DeliveryOrderStatus? status,
            DateOnly? from,
            DateOnly? to,
            IQueryHandler<GetDeliveryOrdersQuery, PagedList<DeliveryOrderResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetDeliveryOrdersQuery(
                new PageRequest(page, pageSize, search), branchId, customerId, salesOrderId, status, from, to);

            Result<PagedList<DeliveryOrderResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesRead);

        group.MapGet("delivery-orders/{deliveryOrderId:guid}", async (
            Guid deliveryOrderId,
            IQueryHandler<GetDeliveryOrderByIdQuery, DeliveryOrderResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<DeliveryOrderResponse> result = await handler.Handle(new GetDeliveryOrderByIdQuery(deliveryOrderId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesRead);

        group.MapPost("delivery-orders", async (
            CreateDeliveryOrderCommand command,
            ICommandHandler<CreateDeliveryOrderCommand, CreateDeliveryOrderResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CreateDeliveryOrderResponse> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesDeliver)
        .WithIdempotency();

        group.MapPost("delivery-orders/{deliveryOrderId:guid}/cancel", async (
            Guid deliveryOrderId,
            ReasonRequest request,
            ICommandHandler<CancelDeliveryOrderCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new CancelDeliveryOrderCommand(deliveryOrderId, request.Reason), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesDeliver);
    }

    private static void MapInvoices(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            Guid? customerId,
            SalesInvoiceStatus? status,
            DateOnly? from,
            DateOnly? to,
            IQueryHandler<GetSalesInvoicesQuery, PagedList<SalesInvoiceResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetSalesInvoicesQuery(new PageRequest(page, pageSize, search), branchId, customerId, status, from, to);

            Result<PagedList<SalesInvoiceResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesRead);

        group.MapGet("{salesInvoiceId:guid}", async (
            Guid salesInvoiceId,
            IQueryHandler<GetSalesInvoiceByIdQuery, SalesInvoiceResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<SalesInvoiceResponse> result = await handler.Handle(new GetSalesInvoiceByIdQuery(salesInvoiceId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesRead);

        group.MapPost("", async (
            CreateSalesInvoiceCommand command,
            ICommandHandler<CreateSalesInvoiceCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(id => Results.Ok(new { Id = id }), CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesInvoice)
        .WithIdempotency();

        group.MapPost("{salesInvoiceId:guid}/post", async (
            Guid salesInvoiceId,
            ICommandHandler<PostSalesInvoiceCommand, string> handler,
            CancellationToken cancellationToken) =>
        {
            Result<string> result = await handler.Handle(new PostSalesInvoiceCommand(salesInvoiceId), cancellationToken);

            return result.Match(number => Results.Ok(new { Number = number }), CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesInvoice);

        group.MapPost("{salesInvoiceId:guid}/cancel", async (
            Guid salesInvoiceId,
            ReasonRequest request,
            ICommandHandler<CancelSalesInvoiceCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new CancelSalesInvoiceCommand(salesInvoiceId, request.Reason), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesInvoice);
    }
}
