using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Finance.Receivables;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class ReceivableEndpoints : IEndpoint
{
    public sealed record ApplyAdvanceRequest(DateOnly Date, IReadOnlyList<ReceiptAllocationRequest> Allocations);

    public sealed record VoidRequest(DateOnly Date, string Reason);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        MapReceipts(app.MapGroup("finance/customer-receipts").WithTags(Tags.CustomerReceipts));
        MapReports(app.MapGroup("finance/receivables").WithTags(Tags.Receivables));
    }

    private static void MapReceipts(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            Guid? customerId,
            DateOnly? from,
            DateOnly? to,
            IQueryHandler<GetCustomerReceiptsQuery, PagedList<CustomerReceiptResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetCustomerReceiptsQuery(new PageRequest(page, pageSize, search), branchId, customerId, from, to);

            Result<PagedList<CustomerReceiptResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ReceivablesRead);

        group.MapGet("{customerReceiptId:guid}", async (
            Guid customerReceiptId,
            IQueryHandler<GetCustomerReceiptByIdQuery, CustomerReceiptResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CustomerReceiptResponse> result = await handler.Handle(
                new GetCustomerReceiptByIdQuery(customerReceiptId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ReceivablesRead);

        group.MapPost("", async (
            CreateCustomerReceiptCommand command,
            ICommandHandler<CreateCustomerReceiptCommand, CreateCustomerReceiptResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CreateCustomerReceiptResponse> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ReceivablesManage)
        .WithIdempotency();

        group.MapPost("{customerReceiptId:guid}/apply-advance", async (
            Guid customerReceiptId,
            ApplyAdvanceRequest request,
            ICommandHandler<ApplyCustomerAdvanceCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new ApplyCustomerAdvanceCommand(customerReceiptId, request.Date, request.Allocations), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.ReceivablesManage);

        group.MapPost("{customerReceiptId:guid}/void", async (
            Guid customerReceiptId,
            VoidRequest request,
            ICommandHandler<VoidCustomerReceiptCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new VoidCustomerReceiptCommand(customerReceiptId, request.Date, request.Reason), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.ReceivablesVoid);
    }

    private static void MapReports(RouteGroupBuilder group)
    {
        group.MapGet("ledger", async (
            Guid customerId,
            DateOnly from,
            DateOnly to,
            Guid? branchId,
            IQueryHandler<GetReceivableLedgerQuery, ReceivableLedgerResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<ReceivableLedgerResponse> result = await handler.Handle(
                new GetReceivableLedgerQuery(customerId, from, to, branchId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ReceivablesRead);

        group.MapGet("aging", async (
            DateOnly asOf,
            Guid? branchId,
            Guid? customerId,
            IQueryHandler<GetReceivableAgingQuery, ReceivableAgingResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<ReceivableAgingResponse> result = await handler.Handle(
                new GetReceivableAgingQuery(asOf, branchId, customerId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.ReceivablesRead);
    }
}
