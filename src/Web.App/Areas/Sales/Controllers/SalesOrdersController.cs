using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Documents;
using Application.Sales;
using Application.TaxCodes.Get;
using Domain.Access;
using Domain.Documents.Attachments;
using Domain.Sales.SalesOrders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.MasterData.Models;
using Web.App.Areas.Sales.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;
using Web.App.Infrastructure.Workflow;

namespace Web.App.Areas.Sales.Controllers;

/// <summary>
/// Sales orders of live birds: Draft (editable) → Approved (credit limit checked; above the limit only with a reason)
/// → Partially delivered / Delivered by delivery orders → Closed; Draft/Approved can be cancelled.
/// </summary>
[Area("Sales")]
[MenuAccess(MenuCodes.SalesOrders)]
public sealed class SalesOrdersController(
    PageSupport support,
    ItemOptions items,
    DisplayFormatter formatter,
    IQueryHandler<GetSalesOrdersQuery, PagedList<SalesOrderResponse>> ordersQuery,
    IQueryHandler<GetSalesOrderByIdQuery, SalesOrderResponse> orderQuery,
    IQueryHandler<GetTaxCodesQuery, IReadOnlyList<TaxCodeResponse>> taxCodesQuery) : AppController
{
    private const string MenuCode = MenuCodes.SalesOrders;
    private const string DocumentType = "SalesOrder";
    private const string CreditBlockedKey = "SalesOrderCreditBlocked";

    private static readonly ExportColumn<SalesOrderResponse>[] Columns =
    [
        new("Number", o => o.Number, Width: 1.5f),
        new("Date", o => o.OrderDate, ExportFormat.Date, 1),
        new("Branch", o => o.BranchCode, Width: 0.7f),
        new("Customer", o => $"{o.CustomerCode} — {o.CustomerName}", Width: 2.5f),
        new("Delivery", o => o.DeliveryDate, ExportFormat.Date, 1),
        new("Birds", o => o.Birds, ExportFormat.WholeNumber, 0.9f),
        new("Delivered birds", o => o.DeliveredBirds, ExportFormat.WholeNumber, 1),
        new("Delivered kg", o => o.DeliveredWeightKg, ExportFormat.Number, 1),
        new("Estimated amount", o => o.EstimatedAmount, ExportFormat.Money, 1.4f),
        new("Status", o => o.Status, Width: 1.1f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? branch, SalesOrderStatus? status, DateOnly? from, DateOnly? to, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<SalesOrderResponse>> result = await ordersQuery.Handle(
            new GetSalesOrdersQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, null, status, from, to),
            cancellationToken);

        return View(new ListViewModel<SalesOrderResponse>
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
        string? format, string? search, string? branch, SalesOrderStatus? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await support.ExportAsync(format, "Sales Orders", "sales-orders",
            SalesLists.Filters(branchFilter, status?.ToString(), from, to, formatter), Columns,
            (paging, ct) => ordersQuery.Handle(new GetSalesOrdersQuery(paging, branchFilter.BranchId, null, status, from, to), ct),
            search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        [FromServices] IQueryHandler<GetCustomerCreditQuery, CustomerCreditResponse> creditQuery,
        CancellationToken cancellationToken)
    {
        Result<SalesOrderResponse> result = await orderQuery.Handle(new GetSalesOrderByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        SalesOrderResponse order = result.Value;
        Result<CustomerCreditResponse> credit = await creditQuery.Handle(new GetCustomerCreditQuery(order.CustomerId, order.Id), cancellationToken);

        return View(new SalesOrderDetailsViewModel(
            order,
            await support.AttachmentsAsync(order.Documents, cancellationToken),
            credit.IsSuccess ? credit.Value : null,
            TempData[CreditBlockedKey] as string,
            CanEdit: await support.CanAsync(MenuCode, MenuRights.Edit),
            CanDeliver: await support.CanAsync(MenuCodes.SalesDeliveries, MenuRights.Create)));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("Form", await WithOptionsAsync(
            new SalesOrderFormViewModel { OrderDate = formatter.Today(), Lines = [new SalesOrderLineFormInput()] }, cancellationToken));

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        SalesOrderFormViewModel model,
        [FromServices] ICommandHandler<CreateSalesOrderCommand, CreateSalesOrderResponse> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            Result<CreateSalesOrderResponse> result = await handler.Handle(
                new CreateSalesOrderCommand(
                    model.BranchId!.Value, model.CustomerId!.Value, model.OrderDate!.Value, model.DeliveryDate, model.Notes,
                    model.ToLines(), model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Sales order {result.Value.Number} has been created as a draft.");
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
        Result<SalesOrderResponse> result = await orderQuery.Handle(new GetSalesOrderByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        SalesOrderResponse order = result.Value;
        if (order.Status != nameof(SalesOrderStatus.Draft))
        {
            NotifyError("Only draft sales orders can be changed.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new SalesOrderFormViewModel
        {
            Id = order.Id,
            Number = order.Number,
            BranchId = order.BranchId,
            CustomerId = order.CustomerId,
            CustomerLabel = $"{order.CustomerCode} — {order.CustomerName}",
            OrderDate = order.OrderDate,
            DeliveryDate = order.DeliveryDate,
            Notes = order.Notes,
            Lines =
            [
                .. (order.Lines ?? []).Select(l => new SalesOrderLineFormInput
                {
                    ItemId = l.ItemId,
                    ItemLabel = $"{l.ItemCode} — {l.ItemName}",
                    Birds = l.Birds,
                    EstimatedWeightKg = l.EstimatedWeightKg,
                    PricePerKg = l.PricePerKg,
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
        SalesOrderFormViewModel model,
        [FromServices] ICommandHandler<UpdateSalesOrderCommand> handler,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && model.Id is Guid id)
        {
            Result result = await handler.Handle(
                new UpdateSalesOrderCommand(id, model.OrderDate!.Value, model.DeliveryDate, model.Notes, model.ToLines(), model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess("The sales order has been saved.");
                return RedirectToAction(nameof(Details), new { id });
            }

            AddErrors(result.Error);
        }

        return View("Form", await WithOptionsAsync(model, cancellationToken));
    }

    /// <summary>
    /// Approves within the customer's credit limit; above it the page offers the approval with a reason.
    /// </summary>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<ApproveSalesOrderCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "approve"),
            ct => handler.Handle(new ApproveSalesOrderCommand(id, null), ct),
            cancellationToken);

        if (result.IsFailure && result.Error.Code == "SalesOrders.CreditLimitExceeded")
        {
            TempData[CreditBlockedKey] = result.Error.Description;
            return RedirectToAction(nameof(Details), new { id });
        }

        return AfterAction(id, result, "The sales order has been approved; deliveries can now be made.");
    }

    /// <summary>
    /// Credit override: approval above the customer's credit limit, with the reason kept on the order.
    /// </summary>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("approve-over-limit")]
    public async Task<IActionResult> ApproveOverLimit(
        Guid id,
        string? reason,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<ApproveSalesOrderCommand> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            NotifyError("Enter the reason for approving above the credit limit.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "approve-over-limit"),
            ct => handler.Handle(new ApproveSalesOrderCommand(id, reason), ct),
            cancellationToken);

        return AfterAction(id, result, "The sales order has been approved above the credit limit.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        string? reason,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<CancelSalesOrderCommand> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            NotifyError("Enter the reason for cancelling.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "cancel"),
            ct => handler.Handle(new CancelSalesOrderCommand(id, reason), ct),
            cancellationToken);

        return AfterAction(id, result, "The sales order has been cancelled.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("close")]
    public async Task<IActionResult> Close(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<CloseSalesOrderCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "close"),
            ct => handler.Handle(new CloseSalesOrderCommand(id), ct),
            cancellationToken);

        return AfterAction(id, result, "The sales order has been closed; the remaining birds are no longer expected.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Documents(
        Guid id,
        List<Guid> documents,
        [FromServices] ICommandHandler<SetDocumentsCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new SetDocumentsCommand(AttachmentOwnerTypes.SalesOrder, id, documents), cancellationToken);
        return AfterAction(id, result, "The attachments have been saved.");
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

    private async Task<SalesOrderFormViewModel> WithOptionsAsync(SalesOrderFormViewModel model, CancellationToken cancellationToken)
    {
        model.VatCodes = await SalesLists.VatCodesAsync(taxCodesQuery, model.Lines.Select(l => l.TaxCodeId), cancellationToken);
        model.Branches = await support.BranchOptionsAsync(model.BranchId);
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);

        foreach (SalesOrderLineFormInput line in model.Lines)
        {
            line.ItemLabel ??= await items.LabelAsync(line.ItemId, cancellationToken);
        }

        return model;
    }
}
