using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Documents;
using Application.Procurement;
using Application.TaxCodes.Get;
using Domain.Access;
using Domain.Documents.Attachments;
using Domain.Procurement.PurchaseOrders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.MasterData.Models;
using Web.App.Areas.Procurement.Documents;
using Web.App.Areas.Procurement.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;
using Web.App.Infrastructure.Workflow;

namespace Web.App.Areas.Procurement.Controllers;

/// <summary>
/// Purchase orders for sapronak: Draft (editable) → Approved → Partially received / Received (by goods receipts)
/// → Closed (remaining quantity no longer expected); Draft/Approved can be cancelled.
/// </summary>
[Area("Procurement")]
[MenuAccess(MenuCodes.ProcurementPurchaseOrders)]
public sealed class PurchaseOrdersController(
    PageSupport support,
    ItemOptions items,
    DisplayFormatter formatter,
    IQueryHandler<GetPurchaseOrdersQuery, PagedList<PurchaseOrderResponse>> ordersQuery,
    IQueryHandler<GetPurchaseOrderByIdQuery, PurchaseOrderResponse> orderQuery,
    IQueryHandler<GetTaxCodesQuery, IReadOnlyList<TaxCodeResponse>> taxCodesQuery) : AppController
{
    private const string MenuCode = MenuCodes.ProcurementPurchaseOrders;
    private const string DocumentType = "PurchaseOrder";

    private static readonly ExportColumn<PurchaseOrderResponse>[] Columns =
    [
        new("Number", o => o.Number, Width: 1.4f),
        new("Date", o => o.OrderDate, ExportFormat.Date, 1),
        new("Branch", o => o.BranchCode, Width: 0.8f),
        new("Vendor", o => $"{o.VendorCode} — {o.VendorName}", Width: 3),
        new("Expected", o => o.ExpectedDate, ExportFormat.Date, 1),
        new("Status", o => o.Status, Width: 1.2f),
        new("Subtotal (excl. VAT)", o => o.Subtotal, ExportFormat.Money, 1.5f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? branch, PurchaseOrderStatus? status, Guid? vendorId, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<PurchaseOrderResponse>> result = await ordersQuery.Handle(
            new GetPurchaseOrdersQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, vendorId, status),
            cancellationToken);

        return View(new ListViewModel<PurchaseOrderResponse>
        {
            Rows = result.Value,
            Search = search,
            BranchOptions = branchFilter.Options,
            Filters = new Dictionary<string, string?> { ["vendorId"] = vendorId?.ToString() },
            FilterOptions = new Dictionary<string, IReadOnlyList<SelectListItem>> { ["status"] = EnumOptions.For(status) }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(
        string? format, string? search, string? branch, PurchaseOrderStatus? status, Guid? vendorId, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        List<string> filters = [branchFilter.Description];
        if (status is not null)
        {
            filters.Add($"Status: {EnumOptions.Label(status.Value.ToString())}");
        }

        return await support.ExportAsync(format, "Purchase Orders", "purchase-orders", filters, Columns,
            (paging, ct) => ordersQuery.Handle(new GetPurchaseOrdersQuery(paging, branchFilter.BranchId, vendorId, status), ct),
            search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<PurchaseOrderResponse> result = await orderQuery.Handle(new GetPurchaseOrderByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        PurchaseOrderResponse order = result.Value;
        (IReadOnlyDictionary<int, string> lineTaxCodes, decimal vat) = await TaxAsync(order, cancellationToken);

        return View(new PurchaseOrderDetailsViewModel(
            order,
            await support.AttachmentsAsync(order.Documents, cancellationToken),
            lineTaxCodes,
            vat,
            CanEdit: await support.CanAsync(MenuCode, MenuRights.Edit),
            CanReceive: await support.CanAsync(MenuCodes.InventoryGoodsReceipts, MenuRights.Create),
            CanPrint: await support.CanAsync(MenuCode, MenuRights.Export)));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("Form", await WithOptionsAsync(
            new PurchaseOrderFormViewModel { OrderDate = formatter.Today(), Lines = [new PurchaseOrderLineFormInput()] },
            cancellationToken));

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        PurchaseOrderFormViewModel model,
        [FromServices] ICommandHandler<CreatePurchaseOrderCommand, CreatePurchaseOrderResponse> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<CreatePurchaseOrderResponse> result = await handler.Handle(
                new CreatePurchaseOrderCommand(
                    model.BranchId!.Value, model.VendorId!.Value, model.OrderDate!.Value, model.ExpectedDate, model.Notes,
                    model.ToLines(), model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Purchase order {result.Value.Number} has been created as a draft.");
                return RedirectToAction(nameof(Details), new { id = result.Value.Id });
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        Result<PurchaseOrderResponse> result = await orderQuery.Handle(new GetPurchaseOrderByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        PurchaseOrderResponse order = result.Value;
        if (order.Status != nameof(PurchaseOrderStatus.Draft))
        {
            NotifyError("Only draft purchase orders can be changed.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new PurchaseOrderFormViewModel
        {
            Id = order.Id,
            Number = order.Number,
            BranchId = order.BranchId,
            VendorId = order.VendorId,
            VendorLabel = $"{order.VendorCode} — {order.VendorName}",
            OrderDate = order.OrderDate,
            ExpectedDate = order.ExpectedDate,
            Notes = order.Notes,
            Lines =
            [
                .. (order.Lines ?? []).Select(l => new PurchaseOrderLineFormInput
                {
                    ItemId = l.ItemId,
                    ItemLabel = $"{l.ItemCode} — {l.ItemName}",
                    UomId = l.UomId,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    TaxCodeId = l.TaxCodeId
                })
            ],
            Documents = [.. order.Documents]
        };

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Edit(
        PurchaseOrderFormViewModel model,
        [FromServices] ICommandHandler<UpdatePurchaseOrderCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdatePurchaseOrderCommand(id, model.OrderDate!.Value, model.ExpectedDate, model.Notes, model.ToLines(), model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess("The purchase order has been saved.");
                return RedirectToAction(nameof(Details), new { id });
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<ApprovePurchaseOrderCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "approve"),
            ct => handler.Handle(new ApprovePurchaseOrderCommand(id), ct),
            cancellationToken);

        return AfterAction(id, result, "The purchase order has been approved; goods can now be received.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        string? reason,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<CancelPurchaseOrderCommand> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            NotifyError("Enter the reason for cancelling.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "cancel"),
            ct => handler.Handle(new CancelPurchaseOrderCommand(id, reason), ct),
            cancellationToken);

        return AfterAction(id, result, "The purchase order has been cancelled.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("close")]
    public async Task<IActionResult> Close(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<ClosePurchaseOrderCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "close"),
            ct => handler.Handle(new ClosePurchaseOrderCommand(id), ct),
            cancellationToken);

        return AfterAction(id, result, "The purchase order has been closed; the remaining quantity is no longer expected.");
    }

    /// <summary>
    /// Replaces the attachments of a purchase order in any status except cancelled.
    /// </summary>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Documents(
        Guid id,
        List<Guid> documents,
        [FromServices] ICommandHandler<SetDocumentsCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new SetDocumentsCommand(AttachmentOwnerTypes.PurchaseOrder, id, documents), cancellationToken);

        return AfterAction(id, result, "The attachments have been saved.");
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Print(Guid id, CancellationToken cancellationToken)
    {
        Result<PurchaseOrderResponse> result = await orderQuery.Handle(new GetPurchaseOrderByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        PurchaseOrderResponse order = result.Value;
        (IReadOnlyDictionary<int, string> lineTaxCodes, decimal vat) = await TaxAsync(order, cancellationToken);
        ExportHeader header = await support.Exports.HeaderAsync(
            "Purchase Order", $"po-{order.Number}", [$"No. {order.Number}", $"Status: {EnumOptions.Label(order.Status)}"]);

        return support.Exports.Document(header, container => PurchaseOrderPdf.Compose(container, order, lineTaxCodes, vat, formatter));
    }

    private RedirectToActionResult AfterAction(Guid id, Result result, string successMessage)
    {
        if (result.IsSuccess)
        {
            NotifySuccess(successMessage);
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// VAT code per line and the VAT estimate at the order date (tax base × rate of each line's VAT code).
    /// </summary>
    private async Task<(IReadOnlyDictionary<int, string> LineTaxCodes, decimal Vat)> TaxAsync(
        PurchaseOrderResponse order, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<TaxCodeResponse>> taxCodes = await taxCodesQuery.Handle(new GetTaxCodesQuery(), cancellationToken);
        Dictionary<Guid, TaxCodeResponse> byId = taxCodes.IsSuccess ? taxCodes.Value.ToDictionary(t => t.Id) : [];

        var codes = new Dictionary<int, string>();
        decimal vat = 0;
        foreach (PurchaseOrderLineResponse line in order.Lines ?? [])
        {
            if (line.TaxCodeId is not Guid taxCodeId || !byId.TryGetValue(taxCodeId, out TaxCodeResponse? taxCode))
            {
                continue;
            }

            codes[line.LineNumber] = taxCode.Code;
            TaxRateResponse? rate = taxCode.Rates.Where(r => r.EffectiveFrom <= order.OrderDate).MaxBy(r => r.EffectiveFrom);
            if (rate is not null)
            {
                vat += Math.Round(line.Amount * rate.TaxBaseRatio * rate.RatePercent / 100m, 2, MidpointRounding.AwayFromZero);
            }
        }

        return (codes, vat);
    }

    private async Task<PurchaseOrderFormViewModel> WithOptionsAsync(PurchaseOrderFormViewModel model, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<TaxCodeResponse>> taxCodes = await taxCodesQuery.Handle(new GetTaxCodesQuery(), cancellationToken);
        HashSet<Guid?> chosen = [.. model.Lines.Select(l => l.TaxCodeId)];

        model.VatCodes = taxCodes.IsSuccess
            ? [.. taxCodes.Value
                .Where(t => t.Type == "Vat" && (t.IsActive || chosen.Contains(t.Id)))
                .Select(t => new SelectListItem($"{t.Code} — {t.Name}", t.Id.ToString()))]
            : [];
        model.Branches = await support.BranchOptionsAsync(model.BranchId);
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);

        foreach (PurchaseOrderLineFormInput line in model.Lines)
        {
            line.ItemLabel ??= await items.LabelAsync(line.ItemId, cancellationToken);
            line.Units = await items.UnitsAsync(line.ItemId, line.UomId, cancellationToken);
        }

        return model;
    }
}
