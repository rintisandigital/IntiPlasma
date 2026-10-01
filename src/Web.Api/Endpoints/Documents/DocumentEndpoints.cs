using Application.Abstractions.Messaging;
using Application.Documents;
using Domain.Documents.Attachments;
using Domain.Roles;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Documents;

/// <summary>
/// <c>PUT {resource}/{id}/documents</c>: replaces the attachments of a master or transaction, also after it was
/// approved/posted (surat jalan bertanda tangan, bukti transfer, …). Uses the write permission of the resource.
/// </summary>
internal sealed class DocumentEndpoints : IEndpoint
{
    public sealed record DocumentsRequest(IReadOnlyList<Guid>? Documents);

    private static readonly (string Route, string OwnerType, string Permission, string Tag)[] Resources =
    [
        ("farmers", AttachmentOwnerTypes.Farmer, Permissions.FarmersManage, Tags.Farmers),
        ("coops", AttachmentOwnerTypes.Coop, Permissions.FarmersManage, Tags.Coops),
        ("vendors", AttachmentOwnerTypes.Vendor, Permissions.MasterDataManage, Tags.Vendors),
        ("customers", AttachmentOwnerTypes.Customer, Permissions.MasterDataManage, Tags.Customers),
        ("contracts", AttachmentOwnerTypes.Contract, Permissions.ContractsManage, Tags.Contracts),
        ("cycles", AttachmentOwnerTypes.Cycle, Permissions.ProductionRecord, Tags.Cycles),
        ("production/daily-recordings", AttachmentOwnerTypes.DailyRecording, Permissions.ProductionRecord, Tags.DailyRecordings),
        ("inventory/goods-receipts", AttachmentOwnerTypes.GoodsReceipt, Permissions.InventoryReceive, Tags.GoodsReceipts),
        ("inventory/stock-returns", AttachmentOwnerTypes.StockReturn, Permissions.InventoryReturn, Tags.StockReturns),
        ("inventory/stock-transfers", AttachmentOwnerTypes.StockTransfer, Permissions.InventoryTransfer, Tags.StockTransfers),
        ("purchase-orders", AttachmentOwnerTypes.PurchaseOrder, Permissions.PurchasingManage, Tags.PurchaseOrders),
        ("sales/orders", AttachmentOwnerTypes.SalesOrder, Permissions.SalesManage, Tags.SalesOrders),
        ("sales/delivery-orders", AttachmentOwnerTypes.DeliveryOrder, Permissions.SalesDeliver, Tags.DeliveryOrders),
        ("finance/vendor-invoices", AttachmentOwnerTypes.VendorInvoice, Permissions.PayablesManage, Tags.VendorInvoices),
        ("finance/payment-vouchers", AttachmentOwnerTypes.PaymentVoucher, Permissions.PayablesManage, Tags.PaymentVouchers),
        ("finance/cash-transactions", AttachmentOwnerTypes.CashTransaction, Permissions.CashBankManage, Tags.CashTransactions),
        ("finance/customer-receipts", AttachmentOwnerTypes.CustomerReceipt, Permissions.ReceivablesManage, Tags.CustomerReceipts),
        ("finance/journals", AttachmentOwnerTypes.Journal, Permissions.JournalsCreate, Tags.Journals),
        ("costing/settlements", AttachmentOwnerTypes.PlasmaSettlement, Permissions.SettlementsManage, Tags.PlasmaSettlements)
    ];

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        foreach ((string route, string ownerType, string permission, string tag) in Resources)
        {
            app.MapPut($"{route}/{{id:guid}}/documents", (
                Guid id,
                DocumentsRequest request,
                ICommandHandler<SetDocumentsCommand> handler,
                CancellationToken cancellationToken) =>
                HandleAsync(new SetDocumentsCommand(ownerType, id, request.Documents), handler, cancellationToken))
            .WithTags(tag)
            .HasPermission(permission);
        }

        app.MapPut("cycles/{cycleId:guid}/harvests/{harvestId:guid}/documents", (
            Guid cycleId,
            Guid harvestId,
            DocumentsRequest request,
            ICommandHandler<SetDocumentsCommand> handler,
            CancellationToken cancellationToken) =>
            HandleAsync(
                new SetDocumentsCommand(AttachmentOwnerTypes.Harvest, harvestId, request.Documents, ParentId: cycleId),
                handler,
                cancellationToken))
        .WithTags(Tags.Cycles)
        .HasPermission(Permissions.ProductionRecord);

        app.MapPut("production/daily-recordings/{id:guid}/revisions/{revisionNumber:int}/documents", (
            Guid id,
            int revisionNumber,
            DocumentsRequest request,
            ICommandHandler<SetDocumentsCommand> handler,
            CancellationToken cancellationToken) =>
            HandleAsync(
                new SetDocumentsCommand(AttachmentOwnerTypes.DailyRecordingRevision, id, request.Documents, RevisionNumber: revisionNumber),
                handler,
                cancellationToken))
        .WithTags(Tags.DailyRecordings)
        .HasPermission(Permissions.ProductionRevise);
    }

    private static async Task<IResult> HandleAsync(
        SetDocumentsCommand command,
        ICommandHandler<SetDocumentsCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }
}
