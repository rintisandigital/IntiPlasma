using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Documents;
using Application.Finance.Payables;
using Application.TaxCodes.Get;
using Application.Vendors;
using Application.Vendors.GetById;
using Domain.Access;
using Domain.Documents.Attachments;
using Domain.Finance.Payables;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.Finance.Models;
using Web.App.Areas.MasterData.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;
using Web.App.Infrastructure.Workflow;

namespace Web.App.Areas.Finance.Controllers;

/// <summary>
/// Vendor invoices matched to received, not yet billed goods (3-way match): Draft → Posted (number, payable
/// journaled; a price difference above the vendor's tolerance needs a reason) → Partially paid / Paid by payment
/// vouchers. Only drafts can be cancelled.
/// </summary>
[Area("Finance")]
[MenuAccess(MenuCodes.FinanceVendorInvoices)]
public sealed class VendorInvoicesController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetVendorInvoicesQuery, PagedList<VendorInvoiceResponse>> invoicesQuery,
    IQueryHandler<GetVendorInvoiceByIdQuery, VendorInvoiceResponse> invoiceQuery) : AppController
{
    private const string MenuCode = MenuCodes.FinanceVendorInvoices;
    private const string DocumentType = "VendorInvoice";
    private const string VarianceBlockedKey = "variance-blocked";

    private static readonly ExportColumn<VendorInvoiceResponse>[] Columns =
    [
        new("Number", i => i.Number ?? "(draft)", Width: 1.5f),
        new("Vendor invoice", i => i.VendorInvoiceNumber, Width: 1.4f),
        new("Date", i => i.InvoiceDate, ExportFormat.Date, 1),
        new("Due", i => i.DueDate, ExportFormat.Date, 1),
        new("Branch", i => i.BranchCode, Width: 0.7f),
        new("Vendor", i => $"{i.VendorCode} — {i.VendorName}", Width: 2.3f),
        new("Subtotal", i => i.Subtotal, ExportFormat.Money, 1.3f),
        new("VAT", i => i.VatAmount, ExportFormat.Money, 1.1f),
        new("PPh", i => i.IncomeTaxAmount, ExportFormat.Money, 1.1f),
        new("Total", i => i.Total, ExportFormat.Money, 1.3f),
        new("Outstanding", i => i.Outstanding, ExportFormat.Money, 1.3f),
        new("Status", i => i.Status, Width: 1)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? branch, VendorInvoiceStatus? status, DateOnly? from, DateOnly? to, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<VendorInvoiceResponse>> result = await invoicesQuery.Handle(
            new GetVendorInvoicesQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, null, status, from, to),
            cancellationToken);

        return View(new ListViewModel<VendorInvoiceResponse>
        {
            Rows = result.Value,
            Search = search,
            BranchOptions = branchFilter.Options,
            Filters = DocumentLists.DateFilters(from, to),
            FilterOptions = new Dictionary<string, IReadOnlyList<SelectListItem>> { ["status"] = EnumOptions.For(status) }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(
        string? format, string? search, string? branch, VendorInvoiceStatus? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await support.ExportAsync(format, "Vendor Invoices", "vendor-invoices",
            DocumentLists.Filters(branchFilter, status?.ToString(), from, to, formatter), Columns,
            (paging, ct) => invoicesQuery.Handle(new GetVendorInvoicesQuery(paging, branchFilter.BranchId, null, status, from, to), ct),
            search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<VendorInvoiceResponse> result = await invoiceQuery.Handle(new GetVendorInvoiceByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        return View(new VendorInvoiceDetailsViewModel(
            result.Value,
            await support.AttachmentsAsync(result.Value.Documents, cancellationToken),
            TempData[VarianceBlockedKey] as string,
            CanEdit: await support.CanAsync(MenuCode, MenuRights.Edit),
            CanPay: await support.CanAsync(MenuCodes.FinancePaymentVouchers, MenuRights.Create)));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        Guid? vendorId,
        Guid? branchId,
        [FromServices] IQueryHandler<GetVendorByIdQuery, VendorResponse> vendorQuery,
        [FromServices] IQueryHandler<GetUninvoicedReceiptsQuery, IReadOnlyList<UninvoicedReceiptLineResponse>> receiptsQuery,
        [FromServices] IQueryHandler<GetTaxCodesQuery, IReadOnlyList<TaxCodeResponse>> taxCodesQuery,
        CancellationToken cancellationToken)
    {
        var model = new VendorInvoiceFormViewModel { VendorId = vendorId, BranchId = branchId, InvoiceDate = formatter.Today() };
        await PrepareAsync(model, vendorQuery, receiptsQuery, taxCodesQuery, cancellationToken);

        // Every open receipt line is billed in full at the order price until the user changes it.
        model.Lines =
        [
            .. model.Receipts.Select(r => new BillLineInput
            {
                GoodsReceiptId = r.GoodsReceiptId,
                GoodsReceiptLineNumber = r.GoodsReceiptLineNumber,
                Selected = true,
                Quantity = r.UninvoicedQuantity,
                UnitPrice = r.OrderUnitPrice
            })
        ];

        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        VendorInvoiceFormViewModel model,
        [FromServices] ICommandHandler<CreateVendorInvoiceCommand, Guid> handler,
        [FromServices] IQueryHandler<GetVendorByIdQuery, VendorResponse> vendorQuery,
        [FromServices] IQueryHandler<GetUninvoicedReceiptsQuery, IReadOnlyList<UninvoicedReceiptLineResponse>> receiptsQuery,
        [FromServices] IQueryHandler<GetTaxCodesQuery, IReadOnlyList<TaxCodeResponse>> taxCodesQuery,
        CancellationToken cancellationToken)
    {
        VendorInvoiceLineRequest[] lines = model.ToLines();
        if (lines.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Tick at least one received line to bill.");
        }

        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateVendorInvoiceCommand(
                    model.BranchId!.Value, model.VendorId!.Value, model.VendorInvoiceNumber, model.TaxInvoiceNumber, model.InvoiceDate!.Value,
                    model.IncomeTaxCodeId, model.Notes, lines, model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess("The draft vendor invoice has been recorded; review it and post it to open the payable.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }

            AddErrors(result.Error);
        }

        await PrepareAsync(model, vendorQuery, receiptsQuery, taxCodesQuery, cancellationToken);
        return View("Form", model);
    }

    /// <summary>
    /// Posts within the vendor's price tolerance; above it the page offers posting with a reason.
    /// </summary>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("post")]
    public async Task<IActionResult> Post(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<PostVendorInvoiceCommand, string> handler,
        CancellationToken cancellationToken)
    {
        (Result result, string? number) = await PostAsync(id, null, "post", workflow, handler, cancellationToken);

        if (result.IsFailure && result.Error.Code == "VendorInvoices.PriceVarianceAboveTolerance")
        {
            TempData[VarianceBlockedKey] = result.Error.Description;
            return RedirectToAction(nameof(Details), new { id });
        }

        return AfterAction(id, result, $"Vendor invoice {number} has been posted; the payable is now open.");
    }

    /// <summary>
    /// Posts with a price difference above the vendor's tolerance. In the API this needs the
    /// <c>payables:approve-variance</c> permission; here the Edit right and a reason (PLAN-WEBAPP W-4).
    /// </summary>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("post-with-variance")]
    public async Task<IActionResult> PostWithVariance(
        Guid id,
        string? reason,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<PostVendorInvoiceCommand, string> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            NotifyError("Enter the reason for accepting the price difference.");
            return RedirectToAction(nameof(Details), new { id });
        }

        (Result result, string? number) = await PostAsync(id, reason, "post-with-variance", workflow, handler, cancellationToken);

        return AfterAction(id, result, $"Vendor invoice {number} has been posted with its price difference.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        string? reason,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<CancelVendorInvoiceCommand> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            NotifyError("Enter the reason for cancelling.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "cancel"),
            ct => handler.Handle(new CancelVendorInvoiceCommand(id, reason), ct),
            cancellationToken);

        return AfterAction(id, result, "The draft vendor invoice has been cancelled; its receipt lines can be billed again.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Documents(
        Guid id,
        List<Guid> documents,
        [FromServices] ICommandHandler<SetDocumentsCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new SetDocumentsCommand(AttachmentOwnerTypes.VendorInvoice, id, documents), cancellationToken);
        return AfterAction(id, result, "The attachments have been saved.");
    }

    private static async Task<(Result Result, string? Number)> PostAsync(
        Guid id,
        string? reason,
        string action,
        IWorkflowActionService workflow,
        ICommandHandler<PostVendorInvoiceCommand, string> handler,
        CancellationToken cancellationToken)
    {
        string? number = null;
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, action),
            async ct =>
            {
                Result<string> posted = await handler.Handle(new PostVendorInvoiceCommand(id, reason), ct);
                number = posted.IsSuccess ? posted.Value : null;
                return posted.IsSuccess ? Result.Success() : Result.Failure(posted.Error);
            },
            cancellationToken);

        return (result, number);
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

    private async Task PrepareAsync(
        VendorInvoiceFormViewModel model,
        IQueryHandler<GetVendorByIdQuery, VendorResponse> vendorQuery,
        IQueryHandler<GetUninvoicedReceiptsQuery, IReadOnlyList<UninvoicedReceiptLineResponse>> receiptsQuery,
        IQueryHandler<GetTaxCodesQuery, IReadOnlyList<TaxCodeResponse>> taxCodesQuery,
        CancellationToken cancellationToken)
    {
        model.Branches = await support.BranchOptionsAsync(model.BranchId);
        model.BranchId ??= Guid.TryParse(model.Branches.FirstOrDefault(b => b.Selected)?.Value, out Guid active) ? active : null;
        model.IncomeTaxCodes = await FinanceOptions.IncomeTaxCodesAsync(taxCodesQuery, model.IncomeTaxCodeId, cancellationToken);
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);

        if (model.VendorId is not Guid vendorId)
        {
            return;
        }

        Result<VendorResponse> vendor = await vendorQuery.Handle(new GetVendorByIdQuery(vendorId), cancellationToken);
        model.VendorLabel = vendor.IsSuccess ? $"{vendor.Value.Code} — {vendor.Value.Name}" : null;

        if (model.BranchId is Guid branchId)
        {
            Result<IReadOnlyList<UninvoicedReceiptLineResponse>> receipts = await receiptsQuery.Handle(
                new GetUninvoicedReceiptsQuery(vendorId, branchId), cancellationToken);
            model.Receipts = receipts.IsSuccess ? receipts.Value : [];
        }
    }
}
