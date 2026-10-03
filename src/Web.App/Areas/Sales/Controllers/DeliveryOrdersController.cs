using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Sales;
using Domain.Access;
using Domain.Sales.DeliveryOrders;
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
/// Delivery orders (surat jalan): harvested trucks sold on an approved sales order, weighed kg × order price. A
/// delivery not invoiced yet can be cancelled.
/// </summary>
[Area("Sales")]
[MenuAccess(MenuCodes.SalesDeliveries)]
public sealed class DeliveryOrdersController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetDeliveryOrdersQuery, PagedList<DeliveryOrderResponse>> deliveriesQuery,
    IQueryHandler<GetDeliveryOrderByIdQuery, DeliveryOrderResponse> deliveryQuery,
    IQueryHandler<GetSalesOrderByIdQuery, SalesOrderResponse> orderQuery) : AppController
{
    private const string MenuCode = MenuCodes.SalesDeliveries;

    private static readonly ExportColumn<DeliveryOrderResponse>[] Columns =
    [
        new("Number", d => d.Number, Width: 1.5f),
        new("Date", d => d.DeliveryDate, ExportFormat.Date, 1),
        new("Branch", d => d.BranchCode, Width: 0.7f),
        new("Sales order", d => d.SalesOrderNumber, Width: 1.5f),
        new("Customer", d => d.CustomerName, Width: 2.2f),
        new("Vehicle", d => d.VehicleNumber, Width: 1),
        new("Birds", d => d.Birds, ExportFormat.WholeNumber, 0.8f),
        new("Weight (kg)", d => d.WeightKg, ExportFormat.Number, 1),
        new("Amount", d => d.Amount, ExportFormat.Money, 1.3f),
        new("Invoice", d => d.SalesInvoiceNumber, Width: 1.5f),
        new("Status", d => d.Status, Width: 0.9f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? branch, DeliveryOrderStatus? status, DateOnly? from, DateOnly? to, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<DeliveryOrderResponse>> result = await deliveriesQuery.Handle(
            new GetDeliveryOrdersQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, null, null, status, from, to),
            cancellationToken);

        return View(new ListViewModel<DeliveryOrderResponse>
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
        string? format, string? search, string? branch, DeliveryOrderStatus? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await support.ExportAsync(format, "Delivery Orders", "delivery-orders",
            SalesLists.Filters(branchFilter, status?.ToString(), from, to, formatter), Columns,
            (paging, ct) => deliveriesQuery.Handle(new GetDeliveryOrdersQuery(paging, branchFilter.BranchId, null, null, status, from, to), ct),
            search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<DeliveryOrderResponse> result = await deliveryQuery.Handle(new GetDeliveryOrderByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        return View(new DeliveryOrderDetailsViewModel(
            result.Value,
            await support.AttachmentsAsync(result.Value.Documents, cancellationToken),
            CanCancel: await support.CanAsync(MenuCode, MenuRights.Edit) && result.Value.Status == nameof(DeliveryOrderStatus.Delivered),
            CanPrint: await support.CanAsync(MenuCode, MenuRights.Export),
            CanInvoice: await support.CanAsync(MenuCodes.SalesInvoices, MenuRights.Create) && result.Value.Status == nameof(DeliveryOrderStatus.Delivered)));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        Guid? salesOrderId,
        [FromServices] IQueryHandler<GetUndeliveredHarvestsQuery, IReadOnlyList<UndeliveredHarvestResponse>> harvestsQuery,
        CancellationToken cancellationToken)
    {
        var model = new DeliveryOrderFormViewModel { SalesOrderId = salesOrderId, DeliveryDate = formatter.Today() };
        await WithOrderAsync(model, harvestsQuery, cancellationToken);
        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        DeliveryOrderFormViewModel model,
        [FromServices] ICommandHandler<CreateDeliveryOrderCommand, CreateDeliveryOrderResponse> handler,
        [FromServices] IQueryHandler<GetUndeliveredHarvestsQuery, IReadOnlyList<UndeliveredHarvestResponse>> harvestsQuery,
        CancellationToken cancellationToken)
    {
        DeliveryOrderLineRequest[] lines =
        [
            .. model.Lines
                .Where(l => l.Selected == true && l.SalesOrderLineNumber is not null)
                .Select(l => new DeliveryOrderLineRequest(l.SalesOrderLineNumber!.Value, l.HarvestId))
        ];

        if (lines.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Tick at least one harvest (truck) to deliver.");
        }

        if (ModelState.IsValid)
        {
            Result<CreateDeliveryOrderResponse> result = await handler.Handle(
                new CreateDeliveryOrderCommand(
                    model.SalesOrderId!.Value, model.DeliveryDate!.Value, model.VehicleNumber, model.DriverName, model.Notes, lines, model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Delivery order {result.Value.Number} has been created.");
                return RedirectToAction(nameof(Details), new { id = result.Value.Id });
            }

            AddErrors(result.Error);
        }

        await WithOrderAsync(model, harvestsQuery, cancellationToken);
        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        string? reason,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<CancelDeliveryOrderCommand> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            NotifyError("Enter the reason for cancelling.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest("DeliveryOrder", id, "cancel"),
            ct => handler.Handle(new CancelDeliveryOrderCommand(id, reason), ct),
            cancellationToken);

        if (result.IsSuccess)
        {
            NotifySuccess("The delivery order has been cancelled; its harvests can be delivered again.");
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Delivery note (surat jalan) for the driver and the customer.
    /// </summary>
    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Print(Guid id, CancellationToken cancellationToken)
    {
        Result<DeliveryOrderResponse> result = await deliveryQuery.Handle(new GetDeliveryOrderByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        DeliveryOrderResponse delivery = result.Value;
        ExportHeader header = await support.Exports.HeaderAsync("Delivery Note", $"do-{delivery.Number}", [$"No. {delivery.Number}"]);

        return support.Exports.Document(header, container => SalesDocumentPdf.DeliveryNote(container, delivery, formatter));
    }

    private async Task WithOrderAsync(
        DeliveryOrderFormViewModel model,
        IQueryHandler<GetUndeliveredHarvestsQuery, IReadOnlyList<UndeliveredHarvestResponse>> harvestsQuery,
        CancellationToken cancellationToken)
    {
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);

        if (model.SalesOrderId is not Guid orderId)
        {
            return;
        }

        Result<SalesOrderResponse> order = await orderQuery.Handle(new GetSalesOrderByIdQuery(orderId), cancellationToken);
        if (order.IsFailure)
        {
            ModelState.AddModelError(string.Empty, order.Error.Description);
            return;
        }

        model.Order = order.Value;
        Result<IReadOnlyList<UndeliveredHarvestResponse>> harvests = await harvestsQuery.Handle(
            new GetUndeliveredHarvestsQuery(order.Value.BranchId, null), cancellationToken);
        model.Harvests = harvests.IsSuccess ? harvests.Value : [];
    }
}
