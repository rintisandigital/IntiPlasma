using Application.Abstractions.Messaging;
using Application.Sales;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
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
/// Nota kredit / retur penjualan on a posted invoice: per-line reductions (price or weight correction) with the VAT
/// corrected accordingly; posted immediately. A void gives the credit back to the invoice and reverses the journal.
/// </summary>
[Area("Sales")]
[MenuAccess(MenuCodes.SalesCreditNotes)]
public sealed class CreditNotesController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetSalesCreditNotesQuery, IReadOnlyList<SalesCreditNoteResponse>> creditNotesQuery,
    IQueryHandler<GetSalesCreditNoteByIdQuery, SalesCreditNoteResponse> creditNoteQuery,
    IQueryHandler<GetSalesInvoiceByIdQuery, SalesInvoiceResponse> invoiceQuery) : AppController
{
    private const string MenuCode = MenuCodes.SalesCreditNotes;
    private const string DocumentType = "SalesCreditNote";

    private static readonly ExportColumn<SalesCreditNoteResponse>[] Columns =
    [
        new("Number", c => c.Number, Width: 1.5f),
        new("Date", c => c.Date, ExportFormat.Date, 1),
        new("Branch", c => c.BranchCode, Width: 0.7f),
        new("Customer", c => c.CustomerName, Width: 2.2f),
        new("Invoice", c => c.InvoiceNumber, Width: 1.5f),
        new("Reason", c => c.Reason, Width: 3),
        new("Subtotal", c => c.Subtotal, ExportFormat.Money, 1.2f),
        new("VAT", c => c.VatAmount, ExportFormat.Money, 1),
        new("Total", c => c.Total, ExportFormat.Money, 1.2f),
        new("Status", c => c.Status.ToString(), Width: 0.8f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? branch, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        IReadOnlyList<SalesCreditNoteResponse> notes = await ListAsync(branchFilter.BranchId, search, cancellationToken);

        return View(new ListViewModel<SalesCreditNoteResponse>
        {
            Rows = new PagedList<SalesCreditNoteResponse>(notes, 1, Math.Max(notes.Count, 1), notes.Count),
            Search = search,
            BranchOptions = branchFilter.Options
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Export(string? format, string? search, string? branch, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await support.ExportAsync(format, "Credit Notes", "credit-notes", [branchFilter.Description], Columns,
            await ListAsync(branchFilter.BranchId, search, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Result<SalesCreditNoteResponse> result = await creditNoteQuery.Handle(new GetSalesCreditNoteByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        return View(new CreditNoteDetailsViewModel(
            result.Value,
            CanEdit: await support.CanAsync(MenuCode, MenuRights.Edit),
            CanPrint: await support.CanAsync(MenuCode, MenuRights.Export)));
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(Guid salesInvoiceId, CancellationToken cancellationToken)
    {
        var model = new CreditNoteFormViewModel { SalesInvoiceId = salesInvoiceId, Date = formatter.Today() };
        if (!await WithInvoiceAsync(model, cancellationToken))
        {
            return NotFound();
        }

        model.Lines = [.. (model.Invoice!.Lines ?? []).Select(l => new CreditLineInput { InvoiceLineNumber = l.LineNumber })];
        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Create)]
    public async Task<IActionResult> Create(
        CreditNoteFormViewModel model,
        [FromServices] ICommandHandler<CreateSalesCreditNoteCommand, CreateSalesCreditNoteResponse> handler,
        CancellationToken cancellationToken)
    {
        CreditNoteLineRequest[] lines =
        [
            .. model.Lines.Where(l => l.Amount > 0).Select(l => new CreditNoteLineRequest(l.InvoiceLineNumber, l.Amount!.Value))
        ];

        if (lines.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Enter the reduction of at least one invoice line.");
        }

        if (ModelState.IsValid)
        {
            Result<CreateSalesCreditNoteResponse> result = await handler.Handle(
                new CreateSalesCreditNoteCommand(model.SalesInvoiceId!.Value, model.Date!.Value, model.Reason, lines), cancellationToken);

            if (result.IsSuccess)
            {
                NotifySuccess($"Credit note {result.Value.Number} of {formatter.Money(result.Value.Total)} has been posted.");
                return RedirectToAction(nameof(Details), new { id = result.Value.Id });
            }

            AddErrors(result.Error);
        }

        await WithInvoiceAsync(model, cancellationToken);
        return View("Form", model);
    }

    [HttpPost]
    [MenuAccess(MenuCode, MenuRights.Edit)]
    [WorkflowAction("void")]
    public async Task<IActionResult> Void(
        Guid id,
        DateOnly? date,
        string? reason,
        [FromServices] IWorkflowActionService workflow,
        [FromServices] ICommandHandler<VoidSalesCreditNoteCommand> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            NotifyError("Enter the reason for voiding.");
            return RedirectToAction(nameof(Details), new { id });
        }

        Result result = await workflow.ExecuteAsync(
            new WorkflowActionRequest(DocumentType, id, "void"),
            ct => handler.Handle(new VoidSalesCreditNoteCommand(id, date ?? formatter.Today(), reason), ct),
            cancellationToken);

        if (result.IsSuccess)
        {
            NotifySuccess("The credit note has been voided; the credit is back on the invoice and its journal reversed.");
        }
        else
        {
            NotifyError(result.Error.Description);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> Print(Guid id, CancellationToken cancellationToken)
    {
        Result<SalesCreditNoteResponse> result = await creditNoteQuery.Handle(new GetSalesCreditNoteByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : NotFound();
        }

        SalesCreditNoteResponse note = result.Value;
        ExportHeader header = await support.Exports.HeaderAsync("Credit Note", $"credit-note-{note.Number}", [$"No. {note.Number}"]);

        return support.Exports.Document(header, container => SalesDocumentPdf.CreditNote(container, note, formatter));
    }

    private async Task<IReadOnlyList<SalesCreditNoteResponse>> ListAsync(Guid? branchId, string? search, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<SalesCreditNoteResponse>> result = await creditNotesQuery.Handle(
            new GetSalesCreditNotesQuery(null, null, branchId), cancellationToken);

        if (result.IsFailure)
        {
            return [];
        }

        return string.IsNullOrWhiteSpace(search)
            ? result.Value
            : [.. result.Value.Where(c =>
                c.Number.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                c.InvoiceNumber.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                c.CustomerName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))];
    }

    private async Task<bool> WithInvoiceAsync(CreditNoteFormViewModel model, CancellationToken cancellationToken)
    {
        Result<SalesInvoiceResponse> invoice = await invoiceQuery.Handle(new GetSalesInvoiceByIdQuery(model.SalesInvoiceId!.Value), cancellationToken);
        model.Invoice = invoice.IsSuccess ? invoice.Value : null;
        return invoice.IsSuccess;
    }
}
