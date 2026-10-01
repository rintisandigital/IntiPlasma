using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Finance.Payables;
using Domain.Finance.Payables;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class PayableEndpoints : IEndpoint
{
    public sealed record ReasonRequest(string Reason);

    public sealed record PayRequest(DateOnly PaymentDate);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        MapVendorInvoices(app.MapGroup("finance/vendor-invoices").WithTags(Tags.VendorInvoices));
        MapPaymentVouchers(app.MapGroup("finance/payment-vouchers").WithTags(Tags.PaymentVouchers));
        MapReports(app.MapGroup("finance/payables").WithTags(Tags.Payables));
    }

    private static void MapVendorInvoices(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            Guid? vendorId,
            VendorInvoiceStatus? status,
            DateOnly? from,
            DateOnly? to,
            IQueryHandler<GetVendorInvoicesQuery, PagedList<VendorInvoiceResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetVendorInvoicesQuery(new PageRequest(page, pageSize, search), branchId, vendorId, status, from, to);

            Result<PagedList<VendorInvoiceResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.PayablesRead);

        group.MapGet("{vendorInvoiceId:guid}", async (
            Guid vendorInvoiceId,
            IQueryHandler<GetVendorInvoiceByIdQuery, VendorInvoiceResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<VendorInvoiceResponse> result = await handler.Handle(new GetVendorInvoiceByIdQuery(vendorInvoiceId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.PayablesRead);

        group.MapGet("uninvoiced-receipts", async (
            Guid vendorId,
            Guid? branchId,
            IQueryHandler<GetUninvoicedReceiptsQuery, IReadOnlyList<UninvoicedReceiptLineResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<UninvoicedReceiptLineResponse>> result = await handler.Handle(
                new GetUninvoicedReceiptsQuery(vendorId, branchId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.PayablesRead);

        group.MapPost("", async (
            CreateVendorInvoiceCommand command,
            ICommandHandler<CreateVendorInvoiceCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(id => Results.Ok(new { Id = id }), CustomResults.Problem);
        })
        .HasPermission(Permissions.PayablesManage)
        .WithIdempotency();

        group.MapPost("{vendorInvoiceId:guid}/post", async (
            Guid vendorInvoiceId,
            ICommandHandler<PostVendorInvoiceCommand, string> handler,
            CancellationToken cancellationToken) =>
        {
            Result<string> result = await handler.Handle(new PostVendorInvoiceCommand(vendorInvoiceId, null), cancellationToken);

            return result.Match(number => Results.Ok(new { Number = number }), CustomResults.Problem);
        })
        .HasPermission(Permissions.PayablesManage);

        group.MapPost("{vendorInvoiceId:guid}/post-with-variance", async (
            Guid vendorInvoiceId,
            ReasonRequest request,
            ICommandHandler<PostVendorInvoiceCommand, string> handler,
            CancellationToken cancellationToken) =>
        {
            Result<string> result = await handler.Handle(new PostVendorInvoiceCommand(vendorInvoiceId, request.Reason), cancellationToken);

            return result.Match(number => Results.Ok(new { Number = number }), CustomResults.Problem);
        })
        .HasPermission(Permissions.PayablesApproveVariance);

        group.MapPost("{vendorInvoiceId:guid}/cancel", async (
            Guid vendorInvoiceId,
            ReasonRequest request,
            ICommandHandler<CancelVendorInvoiceCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new CancelVendorInvoiceCommand(vendorInvoiceId, request.Reason), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.PayablesManage);
    }

    private static void MapPaymentVouchers(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            string? search,
            int? page,
            int? pageSize,
            Guid? branchId,
            Guid? vendorId,
            PaymentVoucherStatus? status,
            DateOnly? from,
            DateOnly? to,
            IQueryHandler<GetPaymentVouchersQuery, PagedList<PaymentVoucherResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetPaymentVouchersQuery(new PageRequest(page, pageSize, search), branchId, vendorId, status, from, to);

            Result<PagedList<PaymentVoucherResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.PayablesRead);

        group.MapGet("{paymentVoucherId:guid}", async (
            Guid paymentVoucherId,
            IQueryHandler<GetPaymentVoucherByIdQuery, PaymentVoucherResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PaymentVoucherResponse> result = await handler.Handle(new GetPaymentVoucherByIdQuery(paymentVoucherId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.PayablesRead);

        group.MapPost("", async (
            CreatePaymentVoucherCommand command,
            ICommandHandler<CreatePaymentVoucherCommand, CreatePaymentVoucherResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CreatePaymentVoucherResponse> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.PayablesManage)
        .WithIdempotency();

        group.MapPost("{paymentVoucherId:guid}/approve", async (
            Guid paymentVoucherId,
            ICommandHandler<ApprovePaymentVoucherCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new ApprovePaymentVoucherCommand(paymentVoucherId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.PayablesApprove);

        group.MapPost("{paymentVoucherId:guid}/pay", async (
            Guid paymentVoucherId,
            PayRequest request,
            ICommandHandler<PayPaymentVoucherCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new PayPaymentVoucherCommand(paymentVoucherId, request.PaymentDate), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.PayablesPay);

        group.MapPost("{paymentVoucherId:guid}/cancel", async (
            Guid paymentVoucherId,
            ReasonRequest request,
            ICommandHandler<CancelPaymentVoucherCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new CancelPaymentVoucherCommand(paymentVoucherId, request.Reason), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.PayablesManage);
    }

    private static void MapReports(RouteGroupBuilder group)
    {
        group.MapGet("ledger", async (
            Guid vendorId,
            DateOnly from,
            DateOnly to,
            Guid? branchId,
            IQueryHandler<GetPayableLedgerQuery, PayableLedgerResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PayableLedgerResponse> result = await handler.Handle(
                new GetPayableLedgerQuery(vendorId, from, to, branchId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.PayablesRead);

        group.MapGet("aging", async (
            DateOnly asOf,
            Guid? branchId,
            Guid? vendorId,
            IQueryHandler<GetPayableAgingQuery, PayableAgingResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<PayableAgingResponse> result = await handler.Handle(
                new GetPayableAgingQuery(asOf, branchId, vendorId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.PayablesRead);
    }
}
