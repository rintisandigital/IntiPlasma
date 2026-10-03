using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Inventory;
using Application.Inventory.GoodsReceipts;
using Application.Procurement;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Inventory.Documents;
using Web.App.Areas.Inventory.Models;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Inventory.Controllers;

/// <summary>
/// Goods receipts (BPB) against approved purchase orders: stock enters the warehouse at the order price and the
/// order's received quantities are updated. Posted documents cannot be changed.
/// </summary>
[Area("Inventory")]
[MenuAccess(MenuCodes.InventoryGoodsReceipts)]
public sealed class GoodsReceiptsController(
    PageSupport support,
    InventoryOptions options,
    DisplayFormatter formatter,
    IQueryHandler<GetGoodsReceiptsQuery, PagedList<InventoryDocumentResponse>> receiptsQuery,
    IQueryHandler<GetGoodsReceiptByIdQuery, InventoryDocumentResponse> receiptQuery,
    IQueryHandler<GetPurchaseOrderByIdQuery, PurchaseOrderResponse> orderQuery) : AppController
{
    private const string MenuCode = MenuCodes.InventoryGoodsReceipts;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? branch, DateOnly? from, DateOnly? to, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<InventoryDocumentResponse>> result = await receiptsQuery.Handle(
            new GetGoodsReceiptsQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, null, null, from, to),
            cancellationToken);

        return View(InventoryLists.ListModel(result.Value, search, branchFilter, from, to));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(
        string? format, string? search, string? branch, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await support.ExportAsync(format, "Goods Receipts", "goods-receipts",
            InventoryLists.Filters(branchFilter, from, to, formatter), InventoryLists.Columns("PO", "Vendor"),
            (paging, ct) => receiptsQuery.Handle(new GetGoodsReceiptsQuery(paging, branchFilter.BranchId, null, null, from, to), ct),
            search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<InventoryDocumentResponse> result = await receiptQuery.Handle(new GetGoodsReceiptByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        return View("~/Areas/Inventory/Views/Shared/Document.cshtml", new InventoryDocumentViewModel(
            result.Value,
            await support.AttachmentsAsync(result.Value.Documents, cancellationToken),
            "Goods Receipt",
            "Purchase order",
            await support.CanAsync(MenuCode, MenuRights.Export)));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(Guid? purchaseOrderId, CancellationToken cancellationToken)
    {
        var model = new GoodsReceiptFormViewModel { PurchaseOrderId = purchaseOrderId, ReceiptDate = formatter.Today() };
        await WithOrderAsync(model, prefill: true, cancellationToken);
        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        GoodsReceiptFormViewModel model,
        [FromServices] ICommandHandler<CreateGoodsReceiptCommand, CreateGoodsReceiptResponse> handler,
        CancellationToken cancellationToken)
    {
        GoodsReceiptLineRequest[] lines =
        [
            .. model.Lines
                .Where(l => l.Quantity > 0)
                .Select(l => new GoodsReceiptLineRequest(l.PurchaseOrderLineNumber, l.Quantity!.Value))
        ];

        if (lines.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Enter the received quantity of at least one line.");
        }

        if (ModelState.IsValid)
        {
            Result<CreateGoodsReceiptResponse> result = await handler.Handle(
                new CreateGoodsReceiptCommand(
                    model.PurchaseOrderId!.Value, model.WarehouseId!.Value, model.ReceiptDate!.Value, model.DeliveryNoteNumber,
                    model.Notes, lines, model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Goods receipt {result.Value.Number} has been posted; the stock is now available.");
                return RedirectToAction(nameof(Details), new { id = result.Value.Id });
            }

            AddErrors(result.Error);
        }

        await WithOrderAsync(model, prefill: false, cancellationToken);
        return View("Form", model);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Print(Guid id, CancellationToken cancellationToken)
    {
        Result<InventoryDocumentResponse> result = await receiptQuery.Handle(new GetGoodsReceiptByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        InventoryDocumentResponse receipt = result.Value;
        ExportHeader header = await support.Exports.HeaderAsync("Goods Receipt (BPB)", $"bpb-{receipt.Number}", [$"No. {receipt.Number}"]);
        (string, string)[] fields =
        [
            ("BPB number", receipt.Number),
            ("Receipt date", formatter.Date(receipt.Date)),
            ("Purchase order", receipt.Reference),
            ("Vendor", receipt.Party ?? "—"),
            ("Warehouse", receipt.WarehouseCode),
            ("Cycle", receipt.CycleNumber ?? "—")
        ];

        return support.Exports.Document(header, container =>
            InventoryDocumentPdf.Compose(container, receipt, fields, ["Delivered by (vendor)", "Received by", "Checked by"], formatter));
    }

    /// <summary>
    /// Loads the order and its outstanding lines; on the first display the received quantity defaults to the
    /// outstanding quantity.
    /// </summary>
    private async Task WithOrderAsync(GoodsReceiptFormViewModel model, bool prefill, CancellationToken cancellationToken)
    {
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);

        if (model.PurchaseOrderId is not Guid orderId)
        {
            return;
        }

        Result<PurchaseOrderResponse> order = await orderQuery.Handle(new GetPurchaseOrderByIdQuery(orderId), cancellationToken);
        if (order.IsFailure)
        {
            ModelState.AddModelError(string.Empty, order.Error.Description);
            return;
        }

        model.Order = order.Value;
        model.Warehouses = await options.WarehousesAsync(order.Value.BranchId, cancellationToken);

        if (prefill)
        {
            model.Lines =
            [
                .. (order.Value.Lines ?? [])
                    .Where(l => l.OutstandingQuantity > 0)
                    .Select(l => new GoodsReceiptLineInput { PurchaseOrderLineNumber = l.LineNumber, Quantity = l.OutstandingQuantity })
            ];
        }
    }
}
