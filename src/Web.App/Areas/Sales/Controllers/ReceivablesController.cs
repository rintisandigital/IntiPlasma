using Application.Abstractions.Messaging;
using Application.Customers;
using Application.Customers.GetById;
using Application.Finance.Receivables;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Sales.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Sales.Controllers;

/// <summary>
/// Kartu piutang (receivable ledger of one customer over a period) and umur piutang (aging per customer and invoice
/// as of a date), with Excel/PDF exports.
/// </summary>
[Area("Sales")]
[MenuAccess(MenuCodes.SalesReceivables)]
public sealed class ReceivablesController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetReceivableLedgerQuery, ReceivableLedgerResponse> ledgerQuery,
    IQueryHandler<GetReceivableAgingQuery, ReceivableAgingResponse> agingQuery,
    IQueryHandler<GetCustomerByIdQuery, CustomerResponse> customerQuery) : AppController
{
    private const string MenuCode = MenuCodes.SalesReceivables;

    private static readonly ExportColumn<ReceivableLedgerLine>[] LedgerColumns =
    [
        new("Date", l => l.Date, ExportFormat.Date, 1),
        new("Document", l => l.DocumentType, Width: 1.1f),
        new("Number", l => l.Number, Width: 1.5f),
        new("Branch", l => l.BranchCode, Width: 0.7f),
        new("Description", l => l.Description, Width: 2.5f),
        new("Debit", l => l.Debit, ExportFormat.Money, 1.3f),
        new("Credit", l => l.Credit, ExportFormat.Money, 1.3f),
        new("Balance", l => l.Balance, ExportFormat.Money, 1.4f)
    ];

    private static readonly ExportColumn<AgingRow>[] AgingColumns =
    [
        new("Customer", r => r.Customer, Width: 2.5f),
        new("Invoice", r => r.Invoice, Width: 1.5f),
        new("Due", r => r.DueDate, ExportFormat.Date, 1),
        new("Days overdue", r => r.DaysOverdue, ExportFormat.WholeNumber, 0.9f),
        new("Current", r => r.Buckets.Current, ExportFormat.Money, 1.2f),
        new("1–30", r => r.Buckets.Days1To30, ExportFormat.Money, 1.2f),
        new("31–60", r => r.Buckets.Days31To60, ExportFormat.Money, 1.2f),
        new("61–90", r => r.Buckets.Days61To90, ExportFormat.Money, 1.2f),
        new("> 90", r => r.Buckets.Over90Days, ExportFormat.Money, 1.2f),
        new("Total", r => r.Buckets.Total, ExportFormat.Money, 1.3f)
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? tab, Guid? customerId, string? branch, DateOnly? from, DateOnly? to, DateOnly? asOf, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch ?? PageSupport.AllBranches);
        DateOnly today = formatter.Today();
        DateOnly start = from ?? new DateOnly(today.Year, today.Month, 1);
        DateOnly end = to ?? today;
        DateOnly agingDate = asOf ?? today;
        string active = tab == "ledger" ? "ledger" : "aging";

        ReceivableLedgerResponse? ledger = null;
        if (active == "ledger" && customerId is Guid customer)
        {
            Result<ReceivableLedgerResponse> result = await ledgerQuery.Handle(
                new GetReceivableLedgerQuery(customer, start, end, branchFilter.BranchId), cancellationToken);
            ledger = result.IsSuccess ? result.Value : null;
        }

        ReceivableAgingResponse? aging = null;
        if (active == "aging")
        {
            Result<ReceivableAgingResponse> result = await agingQuery.Handle(
                new GetReceivableAgingQuery(agingDate, branchFilter.BranchId, customerId), cancellationToken);
            aging = result.IsSuccess ? result.Value : null;
        }

        return View(new ReceivablesViewModel
        {
            Tab = active,
            CustomerId = customerId,
            CustomerLabel = await CustomerLabelAsync(customerId, cancellationToken),
            Branch = branch,
            BranchOptions = branchFilter.Options,
            From = start,
            To = end,
            AsOf = agingDate,
            Ledger = ledger,
            Aging = aging
        });
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> LedgerExport(
        string? format, Guid customerId, string? branch, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch ?? PageSupport.AllBranches);
        Result<ReceivableLedgerResponse> result = await ledgerQuery.Handle(
            new GetReceivableLedgerQuery(customerId, from, to, branchFilter.BranchId), cancellationToken);

        if (result.IsFailure)
        {
            NotifyError(result.Error.Description);
            return RedirectToAction(nameof(Index), new { tab = "ledger", customerId, branch, from, to });
        }

        ReceivableLedgerResponse ledger = result.Value;
        return await support.ExportAsync(format, "Receivable Ledger", "receivable-ledger",
            [
                $"Customer: {ledger.CustomerCode} — {ledger.CustomerName}",
                branchFilter.Description,
                $"Period: {formatter.Date(from)} – {formatter.Date(to)}",
                $"Opening: Rp {formatter.Number(ledger.OpeningBalance)} · Closing: Rp {formatter.Number(ledger.ClosingBalance)}"
            ],
            LedgerColumns, ledger.Lines);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> AgingExport(
        string? format, Guid? customerId, string? branch, DateOnly asOf, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch ?? PageSupport.AllBranches);
        Result<ReceivableAgingResponse> result = await agingQuery.Handle(new GetReceivableAgingQuery(asOf, branchFilter.BranchId, customerId), cancellationToken);

        AgingRow[] rows = result.IsFailure
            ? []
            : [.. result.Value.Customers.SelectMany(c => c.Invoices.Select(i => new AgingRow(
                $"{c.CustomerCode} — {c.CustomerName}", i.Number, i.DueDate, i.DaysOverdue, AgingBuckets.For(i.DaysOverdue, i.Outstanding))))];

        return await support.ExportAsync(format, "Receivable Aging", "receivable-aging",
            [branchFilter.Description, $"As of: {formatter.Date(asOf)}"], AgingColumns, rows);
    }

    private async Task<string?> CustomerLabelAsync(Guid? customerId, CancellationToken cancellationToken)
    {
        if (customerId is not Guid id)
        {
            return null;
        }

        Result<CustomerResponse> customer = await customerQuery.Handle(new GetCustomerByIdQuery(id), cancellationToken);
        return customer.IsSuccess ? $"{customer.Value.Code} — {customer.Value.Name}" : null;
    }

    private sealed record AgingRow(string Customer, string Invoice, DateOnly DueDate, int DaysOverdue, AgingBuckets Buckets);
}
