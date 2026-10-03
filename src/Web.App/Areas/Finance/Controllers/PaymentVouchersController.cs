using Application.Abstractions.Messaging;
using Application.Abstractions.Paging;
using Application.Costing;
using Application.Documents;
using Application.Farmers;
using Application.Farmers.GetById;
using Application.Finance.CashBank;
using Application.Finance.Payables;
using Application.Vendors;
using Application.Vendors.GetById;
using Domain.Access;
using Domain.Costing.PlasmaSettlements;
using Domain.Documents.Attachments;
using Domain.Finance.Payables;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.Finance.Documents;
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
/// Payment vouchers paying posted vendor invoices or approved plasma settlements from a cash/bank account:
/// Draft (maker) → Approved (someone else, checker) → Paid on the actual payment date (journaled); Draft/Approved
/// can be cancelled.
/// </summary>
[Area("Finance")]
[MenuAccess(MenuCodes.FinancePaymentVouchers)]
public sealed class PaymentVouchersController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetPaymentVouchersQuery, PagedList<PaymentVoucherResponse>> vouchersQuery,
    IQueryHandler<GetPaymentVoucherByIdQuery, PaymentVoucherResponse> voucherQuery,
    IQueryHandler<GetCashBankAccountsQuery, IReadOnlyList<CashBankAccountResponse>> cashBankQuery,
    IQueryHandler<GetVendorByIdQuery, VendorResponse> vendorQuery,
    IQueryHandler<GetFarmerByIdQuery, FarmerResponse> farmerQuery,
    IQueryHandler<GetPayableAgingQuery, PayableAgingResponse> agingQuery,
    IQueryHandler<GetPlasmaSettlementsQuery, IReadOnlyList<PlasmaSettlementResponse>> settlementsQuery) : AppController
{
    private const string MenuCode = MenuCodes.FinancePaymentVouchers;
    private const string DocumentType = "PaymentVoucher";
    private const string FarmerPayee = nameof(PayeeType.Farmer);

    private static readonly ExportColumn<PaymentVoucherResponse>[] Columns =
    [
        new("Number", v => v.Number, Width: 1.5f),
        new("Date", v => v.PaymentDate, ExportFormat.Date, 1),
        new("Branch", v => v.BranchCode, Width: 0.7f),
        new("Payee type", v => v.PayeeType == FarmerPayee ? "Plasma" : "Vendor", Width: 0.8f),
        new("Payee", v => $"{v.PayeeCode} — {v.PayeeName}", Width: 2.4f),
        new("Cash/bank", v => v.CashBankCode, Width: 1.1f),
        new("Reference", v => v.Reference, Width: 1.3f),
        new("Amount", v => v.Amount, ExportFormat.Money, 1.3f),
        new("Status", v => v.Status, Width: 0.9f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search, string? branch, PaymentVoucherStatus? status, DateOnly? from, DateOnly? to, int? page, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        Result<PagedList<PaymentVoucherResponse>> result = await vouchersQuery.Handle(
            new GetPaymentVouchersQuery(new PageRequest(page, PageRequest.DefaultPageSize, search), branchFilter.BranchId, null, status, from, to),
            cancellationToken);

        return View(new ListViewModel<PaymentVoucherResponse>
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
        string? format, string? search, string? branch, PaymentVoucherStatus? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await support.ExportAsync(format, "Payment Vouchers", "payment-vouchers",
            DocumentLists.Filters(branchFilter, status?.ToString(), from, to, formatter), Columns,
            (paging, ct) => vouchersQuery.Handle(new GetPaymentVouchersQuery(paging, branchFilter.BranchId, null, status, from, to), ct),
            search, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<PaymentVoucherResponse> result = await voucherQuery.Handle(new GetPaymentVoucherByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        return View(new PaymentVoucherDetailsViewModel(
            result.Value,
            await support.AttachmentsAsync(result.Value.Documents, cancellationToken),
            CanEdit: await support.CanAsync(MenuCode, MenuRights.Edit),
            CanPrint: await support.CanAsync(MenuCode, MenuRights.Export)));
    }

    /// <param name="payeeType">"Vendor" (default) or "Farmer" (plasma settlement payment).</param>
    /// <param name="documentId">Invoice/settlement to pay in full right away (from its detail page).</param>
    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        string? payeeType,
        Guid? payeeId,
        Guid? documentId,
        CancellationToken cancellationToken)
    {
        var model = new PaymentVoucherFormViewModel
        {
            PayeeType = payeeType == FarmerPayee ? FarmerPayee : nameof(PayeeType.Vendor),
            PayeeId = payeeId,
            PaymentDate = formatter.Today()
        };
        await PrepareAsync(model, cancellationToken);

        model.Allocations =
        [
            .. model.OpenDocuments.Select(d => new PayableAllocationInput
            {
                DocumentId = d.DocumentId,
                Amount = d.DocumentId == documentId ? d.Outstanding : null
            })
        ];

        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        PaymentVoucherFormViewModel model,
        [FromServices] ICommandHandler<CreatePaymentVoucherCommand, CreatePaymentVoucherResponse> vendorHandler,
        [FromServices] ICommandHandler<CreatePlasmaPaymentVoucherCommand, CreatePaymentVoucherResponse> plasmaHandler,
        CancellationToken cancellationToken)
    {
        (Guid DocumentId, decimal Amount)[] allocations = model.ToAllocations();
        if (model.PayeeId is null)
        {
            ModelState.AddModelError(nameof(model.PayeeId), "Choose the payee.");
        }

        if (allocations.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Enter the amount to pay on at least one document.");
        }

        if (ModelState.IsValid)
        {
            Result<CreatePaymentVoucherResponse> result = model.IsPlasma
                ? await plasmaHandler.Handle(
                    new CreatePlasmaPaymentVoucherCommand(
                        model.CashBankAccountId!.Value, model.PayeeId!.Value, model.PaymentDate!.Value, model.Reference, model.Notes,
                        [.. allocations.Select(a => new SettlementPaymentRequest(a.DocumentId, a.Amount))], model.Documents),
                    cancellationToken)
                : await vendorHandler.Handle(
                    new CreatePaymentVoucherCommand(
                        model.CashBankAccountId!.Value, model.PayeeId!.Value, model.PaymentDate!.Value, model.Reference, model.Notes,
                        [.. allocations.Select(a => new PaymentAllocationRequest(a.DocumentId, a.Amount))], model.Documents),
                    cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Payment voucher {result.Value.Number} of {formatter.Money(result.Value.Amount)} has been drafted; another user must approve it.");
                return RedirectToAction(nameof(Details), new { id = result.Value.Id });
            }

            AddErrors(result.Error);
        }

        await PrepareAsync(model, cancellationToken);
        return View("Form", model);
    }

    /// <summary>
    /// Checker approval: the domain refuses the maker approving their own voucher.
    /// </summary>
    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<ApprovePaymentVoucherCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "approve"),
            ct => handler.Handle(new ApprovePaymentVoucherCommand(id), ct),
            cancellationToken);

        return AfterAction(id, result, "The payment voucher has been approved; it can now be paid.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("pay")]
    public async Task<IActionResult> Pay(
        Guid id,
        DateOnly? date,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<PayPaymentVoucherCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "pay"),
            ct => handler.Handle(new PayPaymentVoucherCommand(id, date ?? formatter.Today()), ct),
            cancellationToken);

        return AfterAction(id, result, "The payment voucher has been paid; the documents are settled and the payment journaled.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        string? reason,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<CancelPaymentVoucherCommand> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            NotifyError("Enter the reason for cancelling.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "cancel"),
            ct => handler.Handle(new CancelPaymentVoucherCommand(id, reason), ct),
            cancellationToken);

        return AfterAction(id, result, "The payment voucher has been cancelled.");
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    public async Task<IActionResult> Documents(
        Guid id,
        List<Guid> documents,
        [FromServices] ICommandHandler<SetDocumentsCommand> handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new SetDocumentsCommand(AttachmentOwnerTypes.PaymentVoucher, id, documents), cancellationToken);
        return AfterAction(id, result, "The attachments have been saved.");
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Print(Guid id, CancellationToken cancellationToken)
    {
        Result<PaymentVoucherResponse> result = await voucherQuery.Handle(new GetPaymentVoucherByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        PaymentVoucherResponse voucher = result.Value;
        string title = voucher.Status is nameof(PaymentVoucherStatus.Draft) ? "Payment Voucher (DRAFT)" : "Payment Voucher";
        ExportHeader header = await support.Exports.HeaderAsync(title, $"payment-voucher-{voucher.Number}", [$"No. {voucher.Number}"]);

        return support.Exports.Document(header, container => FinanceDocumentPdf.PaymentVoucher(container, voucher, formatter));
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

    private async Task PrepareAsync(PaymentVoucherFormViewModel model, CancellationToken cancellationToken)
    {
        IReadOnlyList<CashBankAccountResponse> accounts = await FinanceOptions.CashBankAccountsAsync(cashBankQuery, null, cancellationToken);
        model.CashBankAccounts = FinanceOptions.CashBankOptions(accounts, model.CashBankAccountId);
        model.Attachments = await support.AttachmentsAsync(model.Documents, cancellationToken);

        if (model.PayeeId is not Guid payeeId)
        {
            return;
        }

        if (model.IsPlasma)
        {
            Result<FarmerResponse> farmer = await farmerQuery.Handle(new GetFarmerByIdQuery(payeeId), cancellationToken);
            model.PayeeLabel = farmer.IsSuccess ? $"{farmer.Value.Code} — {farmer.Value.Name}" : null;
            model.OpenDocuments = await OpenSettlementsAsync(payeeId, cancellationToken);
        }
        else
        {
            Result<VendorResponse> vendor = await vendorQuery.Handle(new GetVendorByIdQuery(payeeId), cancellationToken);
            model.PayeeLabel = vendor.IsSuccess ? $"{vendor.Value.Code} — {vendor.Value.Name}" : null;
            model.OpenDocuments = await OpenInvoicesAsync(payeeId, cancellationToken);
        }
    }

    /// <summary>
    /// Posted invoices of the vendor with an outstanding amount (aging as of today), oldest due first.
    /// </summary>
    private async Task<IReadOnlyList<OpenPayable>> OpenInvoicesAsync(Guid vendorId, CancellationToken cancellationToken)
    {
        Result<PayableAgingResponse> aging = await agingQuery.Handle(new GetPayableAgingQuery(formatter.Today(), null, vendorId), cancellationToken);

        return aging.IsFailure
            ? []
            : [.. aging.Value.Vendors
                .SelectMany(v => v.Invoices)
                .Where(i => i.Outstanding > 0)
                .OrderBy(i => i.DueDate)
                .Select(i => new OpenPayable(
                    i.VendorInvoiceId, i.Number, i.VendorInvoiceNumber, i.BranchCode, i.InvoiceDate, i.DueDate, i.DaysOverdue, i.Total, i.Outstanding))];
    }

    /// <summary>
    /// Approved (or partially paid) settlements of the plasma farmer with an amount still to pay.
    /// </summary>
    private async Task<IReadOnlyList<OpenPayable>> OpenSettlementsAsync(Guid farmerId, CancellationToken cancellationToken)
    {
        var open = new List<PlasmaSettlementResponse>();
        foreach (PlasmaSettlementStatus status in new[] { PlasmaSettlementStatus.Approved, PlasmaSettlementStatus.PartiallyPaid })
        {
            Result<IReadOnlyList<PlasmaSettlementResponse>> result = await settlementsQuery.Handle(
                new GetPlasmaSettlementsQuery(null, farmerId, null, status), cancellationToken);
            if (result.IsSuccess)
            {
                open.AddRange(result.Value.Where(s => s.Outstanding > 0));
            }
        }

        return [.. open
            .OrderBy(s => s.SettlementDate)
            .Select(s => new OpenPayable(s.Id, s.Number, s.CycleNumber, s.BranchCode, s.SettlementDate, null, 0, s.NetPayable, s.Outstanding))];
    }
}
