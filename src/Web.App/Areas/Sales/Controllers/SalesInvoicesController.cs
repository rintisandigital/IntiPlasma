using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Sales;
using Domain.Access;
using Domain.Sales.DeliveryOrders;
using Domain.Sales.SalesInvoices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.MasterData.Models;
using Web.App.Areas.Sales.Documents;
using Web.App.Areas.Sales.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;
using Web.App.Infrastructure.Workflow;

namespace Web.App.Areas.Sales.Controllers;

/// <summary>
/// Sales invoices from delivered delivery orders of one customer: Draft (cancellable) → Posted (number, receivable
/// and estimated cost of goods journaled) → Partially paid / Paid by customer receipts; corrected by credit notes.
/// </summary>
[Area("Sales")]
[MenuAccess(MenuCodes.SalesInvoices)]
public sealed class SalesInvoicesController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetSalesInvoicesQuery, PagedList<SalesInvoiceResponse>> invoicesQuery,
    IQueryHandler<GetSalesInvoiceByIdQuery, SalesInvoiceResponse> invoiceQuery,
    IQueryHandler<GetDeliveryOrdersQuery, PagedList<DeliveryOrderResponse>> deliveriesQuery,
    IQueryHandler<GetDeliveryOrderByIdQuery, DeliveryOrderResponse> deliveryQuery) : AppController
{
    private const string MenuCode = MenuCodes.SalesInvoices;
    private const string DocumentType = "SalesInvoice";

    private static readonly ExportColumn<SalesInvoiceResponse>[] Columns =
    [
        new("Number", i => i.Number ?? "(draft)", Width: 1.5f),
        new("Date", i => i.InvoiceDate, ExportFormat.Date, 1),
        new("Due", i => i.DueDate, ExportFormat.Date, 1),
        new("Branch", i => i.BranchCode, Width: 0.7f),
        new("Customer", i => $"{i.CustomerCode} — {i.CustomerName}", Width: 2.4f),
        new("Subtotal", i => i.Subtotal, ExportFormat.Money, 1.3f),
        new("VAT", i => i.VatAmount, ExportFormat.Money, 1.1f),
        new("Total", i => i.Total, ExportFormat.Money, 1.3f),
        new("Paid", i => i.PaidAmount, ExportFormat.Money, 1.2f),
        new("Credited", i => i.CreditedAmount, ExportFormat.Money, 1.1f),
        new("Outstanding", i => i.Outstanding, ExportFormat.Money, 1.3f),
        new("Status", i => i.Status, Width: 1)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? branch, SalesInvoiceStatus? status, DateOnly? from, DateOnly? to, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<SalesInvoiceResponse>> result = await invoicesQuery.Handle(
            new GetSalesInvoicesQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, null, status, from, to),
            cancellationToken);

        return View(new ListViewModel<SalesInvoiceResponse>
        {
            Rows = result.Value,
            Search = search,
            BranchOptions = branchFilter.Options,
            Filters = SalesLists.DateFilters(from, to),
            FilterOptions = new Dictionary<string, IReadOnlyList<SelectListItem>> { ["status"] = EnumOptions.For(status) }
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(
        string? format, string? search, string? branch, SalesInvoiceStatus? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await support.ExportAsync(format, "Sales Invoices", "sales-invoices",
            SalesLists.Filters(branchFilter, status?.ToString(), from, to, formatter), Columns,
            (paging, ct) => invoicesQuery.Handle(new GetSalesInvoicesQuery(paging, branchFilter.BranchId, null, status, from, to), ct),
            search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        [FromServices] IQueryHandler<GetSalesCreditNotesQuery, IReadOnlyList<SalesCreditNoteResponse>> creditNotesQuery,
        CancellationToken cancellationToken)
    {
        Result<SalesInvoiceResponse> result = await invoiceQuery.Handle(new GetSalesInvoiceByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        Result<IReadOnlyList<SalesCreditNoteResponse>> creditNotes = await creditNotesQuery.Handle(
            new GetSalesCreditNotesQuery(id, null, null), cancellationToken);

        return View(new SalesInvoiceDetailsViewModel(
            result.Value,
            creditNotes.IsSuccess ? creditNotes.Value : [],
            CanEdit: await support.CanAsync(MenuCode, MenuRights.Edit),
            CanPrint: await support.CanAsync(MenuCode, MenuRights.Export),
            CanCredit: await support.CanAsync(MenuCodes.SalesCreditNotes, MenuRights.Create),
            CanReceive: await support.CanAsync(MenuCodes.SalesReceipts, MenuRights.Create)));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(Guid? customerId, Guid? deliveryOrderId, CancellationToken cancellationToken)
    {
        var model = new SalesInvoiceFormViewModel
        {
            CustomerId = customerId,
            InvoiceDate = formatter.Today(),
            DeliveryOrderIds = deliveryOrderId is Guid delivery ? [delivery] : []
        };

        await WithDeliveriesAsync(model, cancellationToken);
        if (model.DeliveryOrderIds.Count == 0)
        {
            model.DeliveryOrderIds = [.. model.Deliveries.Select(d => d.Id)];
        }

        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        SalesInvoiceFormViewModel model,
        [FromServices] ICommandHandler<CreateSalesInvoiceCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        if (model.DeliveryOrderIds.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Tick at least one delivery order.");
        }

        if (ModelState.IsValid)
        {
            Result<Guid> result = await handler.Handle(
                new CreateSalesInvoiceCommand(model.DeliveryOrderIds, model.InvoiceDate!.Value, model.Notes), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess("The draft invoice has been created; review it and post it to bill the customer.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }

            AddErrors(result.Error);
        }

        await WithDeliveriesAsync(model, cancellationToken);
        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("post")]
    public async Task<IActionResult> Post(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<PostSalesInvoiceCommand, string> handler,
        CancellationToken cancellationToken)
    {
        string? number = null;
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "post"),
            async ct =>
            {
                Result<string> posted = await handler.Handle(new PostSalesInvoiceCommand(id), ct);
                number = posted.IsSuccess ? posted.Value : null;
                return posted.IsSuccess ? Result.Success() : Result.Failure(posted.Error);
            },
            cancellationToken);

        return AfterAction(id, result, $"Invoice {number} has been posted; the receivable is now open.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        string? reason,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<CancelSalesInvoiceCommand> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            NotifyError("Enter the reason for cancelling.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "cancel"),
            ct => handler.Handle(new CancelSalesInvoiceCommand(id, reason), ct),
            cancellationToken);

        return AfterAction(id, result, "The draft invoice has been cancelled; its delivery orders can be invoiced again.");
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Print(Guid id, CancellationToken cancellationToken)
    {
        Result<SalesInvoiceResponse> result = await invoiceQuery.Handle(new GetSalesInvoiceByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        SalesInvoiceResponse invoice = result.Value;
        string number = invoice.Number ?? "DRAFT";
        ExportHeader header = await support.Exports.HeaderAsync(
            invoice.Status == nameof(SalesInvoiceStatus.Draft) ? "Sales Invoice (DRAFT)" : "Sales Invoice",
            $"invoice-{number}",
            [$"No. {number}"]);

        return support.Exports.Document(header, container => SalesDocumentPdf.Invoice(container, invoice, formatter));
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
    /// Delivered, not yet invoiced delivery orders of the chosen customer (or of the given delivery's customer).
    /// </summary>
    private async Task WithDeliveriesAsync(SalesInvoiceFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.CustomerId is null && model.DeliveryOrderIds.Count == 1)
        {
            Result<DeliveryOrderResponse> single = await deliveryQuery.Handle(new GetDeliveryOrderByIdQuery(model.DeliveryOrderIds[0]), cancellationToken);
            model.CustomerId = single.IsSuccess ? single.Value.CustomerId : null;
        }

        if (model.CustomerId is not Guid customerId)
        {
            return;
        }

        Result<PagedList<DeliveryOrderResponse>> deliveries = await deliveriesQuery.Handle(
            new GetDeliveryOrdersQuery(new PageRequest(1, PageRequest.MaxPageSize), null, customerId, null, DeliveryOrderStatus.Delivered, null, null),
            cancellationToken);
        model.Deliveries = deliveries.IsSuccess ? deliveries.Value.Items : [];
        model.CustomerLabel ??= model.Deliveries.Count > 0 ? $"{model.Deliveries[0].CustomerCode} — {model.Deliveries[0].CustomerName}" : null;
    }
}
