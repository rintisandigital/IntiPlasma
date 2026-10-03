using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Customers;
using Application.Customers.GetById;
using Application.Finance.CashBank;
using Application.Finance.Receivables;
using Domain.Access;
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
/// Customer receipts into a cash/bank account: allocated to open invoices of the customer and/or kept as an advance
/// (uang muka) applied to invoices later. A receipt is voided (e.g. a bounced giro) instead of being deleted.
/// </summary>
[Area("Sales")]
[MenuAccess(MenuCodes.SalesReceipts)]
public sealed class ReceiptsController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetCustomerReceiptsQuery, PagedList<CustomerReceiptResponse>> receiptsQuery,
    IQueryHandler<GetCustomerReceiptByIdQuery, CustomerReceiptResponse> receiptQuery,
    IQueryHandler<GetReceivableAgingQuery, ReceivableAgingResponse> agingQuery) : AppController
{
    private const string MenuCode = MenuCodes.SalesReceipts;
    private const string DocumentType = "CustomerReceipt";

    private static readonly ExportColumn<CustomerReceiptResponse>[] Columns =
    [
        new("Number", r => r.Number, Width: 1.5f),
        new("Date", r => r.ReceiptDate, ExportFormat.Date, 1),
        new("Branch", r => r.BranchCode, Width: 0.7f),
        new("Customer", r => $"{r.CustomerCode} — {r.CustomerName}", Width: 2.4f),
        new("Cash/bank", r => r.CashBankCode ?? r.CashAccountCode, Width: 1.1f),
        new("Reference", r => r.Reference, Width: 1.3f),
        new("Amount", r => r.Amount, ExportFormat.Money, 1.3f),
        new("Advance", r => r.AdvanceAmount, ExportFormat.Money, 1.2f),
        new("Unapplied advance", r => r.UnappliedAdvance, ExportFormat.Money, 1.2f),
        new("Status", r => r.Status, Width: 0.8f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? branch, DateOnly? from, DateOnly? to, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<CustomerReceiptResponse>> result = await receiptsQuery.Handle(
            new GetCustomerReceiptsQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, null, from, to),
            cancellationToken);

        return View(new ListViewModel<CustomerReceiptResponse>
        {
            Rows = result.Value,
            Search = search,
            BranchOptions = branchFilter.Options,
            Filters = SalesLists.DateFilters(from, to)
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(
        string? format, string? search, string? branch, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await support.ExportAsync(format, "Customer Receipts", "customer-receipts",
            SalesLists.Filters(branchFilter, null, from, to, formatter), Columns,
            (paging, ct) => receiptsQuery.Handle(new GetCustomerReceiptsQuery(paging, branchFilter.BranchId, null, from, to), ct),
            search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<CustomerReceiptResponse> result = await receiptQuery.Handle(new GetCustomerReceiptByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        CustomerReceiptResponse receipt = result.Value;

        return View(new ReceiptDetailsViewModel(
            receipt,
            await support.AttachmentsAsync(receipt.Documents, cancellationToken),
            receipt.UnappliedAdvance > 0 ? await OpenInvoicesAsync(receipt.CustomerId, cancellationToken) : [],
            CanEdit: await support.CanAsync(MenuCode, MenuRights.Edit),
            CanPrint: await support.CanAsync(MenuCode, MenuRights.Export)));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        Guid? customerId,
        Guid? salesInvoiceId,
        [FromServices] IQueryHandler<GetCustomerByIdQuery, CustomerResponse> customerQuery,
        [FromServices] IQueryHandler<GetCashBankAccountsQuery, IReadOnlyList<CashBankAccountResponse>> cashBankQuery,
        CancellationToken cancellationToken)
    {
        var model = new ReceiptFormViewModel { CustomerId = customerId, ReceiptDate = formatter.Today() };
        await PrepareAsync(model, customerQuery, cashBankQuery, cancellationToken);

        // From an invoice: pay its outstanding; otherwise nothing is allocated until the user enters amounts.
        model.Allocations =
        [
            .. model.Invoices.Select(i => new AllocationInput
            {
                SalesInvoiceId = i.SalesInvoiceId,
                Amount = i.SalesInvoiceId == salesInvoiceId ? i.Outstanding : null
            })
        ];

        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        ReceiptFormViewModel model,
        [FromServices] ICommandHandler<CreateCustomerReceiptCommand, CreateCustomerReceiptResponse> handler,
        [FromServices] IQueryHandler<GetCustomerByIdQuery, CustomerResponse> customerQuery,
        [FromServices] IQueryHandler<GetCashBankAccountsQuery, IReadOnlyList<CashBankAccountResponse>> cashBankQuery,
        CancellationToken cancellationToken)
    {
        ReceiptAllocationRequest[] allocations = model.ToAllocations();
        if (allocations.Length == 0 && (model.AdvanceAmount ?? 0) <= 0)
        {
            ModelState.AddModelError(string.Empty, "Allocate the receipt to at least one invoice or enter an advance.");
        }

        if (ModelState.IsValid)
        {
            Result<CreateCustomerReceiptResponse> result = await handler.Handle(
                new CreateCustomerReceiptCommand(
                    model.CashBankAccountId!.Value, model.CustomerId!.Value, model.ReceiptDate!.Value, model.Reference, model.Notes,
                    allocations, model.AdvanceAmount ?? 0, model.Documents),
                cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Receipt {result.Value.Number} of {formatter.Money(result.Value.Amount)} has been posted.");
                return RedirectToAction(nameof(Details), new { id = result.Value.Id });
            }

            AddErrors(result.Error);
        }

        await PrepareAsync(model, customerQuery, cashBankQuery, cancellationToken);
        return View("Form", model);
    }

    /// <summary>
    /// Applies (part of) the receipt's unapplied advance to open invoices of the customer.
    /// </summary>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> ApplyAdvance(
        Guid id,
        DateOnly? date,
        List<AllocationInput> allocations,
        [FromServices] ICommandHandler<ApplyCustomerAdvanceCommand> handler,
        CancellationToken cancellationToken)
    {
        ReceiptAllocationRequest[] requests =
            [.. allocations.Where(a => a.Amount > 0 && a.SalesInvoiceId is not null).Select(a => new ReceiptAllocationRequest(a.SalesInvoiceId!.Value, a.Amount!.Value))];

        if (requests.Length == 0)
        {
            NotifyError("Enter the amount to apply on at least one invoice.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await handler.Handle(new ApplyCustomerAdvanceCommand(id, date ?? formatter.Today(), requests), cancellationToken);
        return AfterAction(id, result, "The advance has been applied to the invoices.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("void")]
    public async Task<IActionResult> Void(
        Guid id,
        DateOnly? date,
        string? reason,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<VoidCustomerReceiptCommand> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            NotifyError("Enter the reason for voiding.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "void"),
            ct => handler.Handle(new VoidCustomerReceiptCommand(id, date ?? formatter.Today(), reason), ct),
            cancellationToken);

        return AfterAction(id, result, "The receipt has been voided; its allocations are back on the invoices and its journal reversed.");
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Print(Guid id, CancellationToken cancellationToken)
    {
        Result<CustomerReceiptResponse> result = await receiptQuery.Handle(new GetCustomerReceiptByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        CustomerReceiptResponse receipt = result.Value;
        ExportHeader header = await support.Exports.HeaderAsync("Official Receipt", $"receipt-{receipt.Number}", [$"No. {receipt.Number}"]);

        return support.Exports.Document(header, container => SalesDocumentPdf.Receipt(container, receipt, formatter));
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
    /// Open posted invoices of the customer with their outstanding amount (aging as of today).
    /// </summary>
    private async Task<IReadOnlyList<InvoiceAging>> OpenInvoicesAsync(Guid customerId, CancellationToken cancellationToken)
    {
        Result<ReceivableAgingResponse> aging = await agingQuery.Handle(new GetReceivableAgingQuery(formatter.Today(), null, customerId), cancellationToken);

        return aging.IsSuccess
            ? [.. aging.Value.Customers.SelectMany(c => c.Invoices).Where(i => i.Outstanding > 0).OrderBy(i => i.DueDate)]
            : [];
    }

    private async Task PrepareAsync(
        ReceiptFormViewModel model,
        IQueryHandler<GetCustomerByIdQuery, CustomerResponse> customerQuery,
        IQueryHandler<GetCashBankAccountsQuery, IReadOnlyList<CashBankAccountResponse>> cashBankQuery,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<CashBankAccountResponse>> accounts = await cashBankQuery.Handle(new GetCashBankAccountsQuery(null, null, false), cancellationToken);
        model.CashBankAccounts = accounts.IsSuccess
            ? [.. accounts.Value.Select(a => new SelectListItem($"{a.Code} — {a.Name} ({a.BranchCode})", a.Id.ToString(), a.Id == model.CashBankAccountId))]
            : [];
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);

        if (model.CustomerId is Guid customerId)
        {
            Result<CustomerResponse> customer = await customerQuery.Handle(new GetCustomerByIdQuery(customerId), cancellationToken);
            model.CustomerLabel = customer.IsSuccess ? $"{customer.Value.Code} — {customer.Value.Name}" : null;
            model.Invoices = await OpenInvoicesAsync(customerId, cancellationToken);
        }
    }
}
