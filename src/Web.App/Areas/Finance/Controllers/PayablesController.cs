using Application.Abstractions.Messaging;
using Application.Finance.Payables;
using Application.Finance.Receivables;
using Application.Vendors;
using Application.Vendors.GetById;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Areas.Finance.Models;
using Web.App.Controllers;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;

namespace Web.App.Areas.Finance.Controllers;

/// <summary>
/// Kartu hutang (payable ledger of one vendor over a period) and umur hutang (aging per vendor and invoice as of a
/// date), with Excel/PDF exports.
/// </summary>
[Area("Finance")]
[MenuAccess(MenuCodes.FinancePayables)]
public sealed class PayablesController(
    PageSupport support,
    DisplayFormatter formatter,
    IQueryHandler<GetPayableLedgerQuery, PayableLedgerResponse> ledgerQuery,
    IQueryHandler<GetPayableAgingQuery, PayableAgingResponse> agingQuery,
    IQueryHandler<GetVendorByIdQuery, VendorResponse> vendorQuery) : AppController
{
    private const string MenuCode = MenuCodes.FinancePayables;

    private static readonly ExportColumn<PayableLedgerLine>[] LedgerColumns =
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
        new("Vendor", r => r.Vendor, Width: 2.5f),
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
        string? tab, Guid? vendorId, string? branch, DateOnly? from, DateOnly? to, DateOnly? asOf, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch ?? PageSupport.AllBranches);
        DateOnly today = formatter.Today();
        DateOnly start = from ?? new DateOnly(today.Year, today.Month, 1);
        DateOnly end = to ?? today;
        DateOnly agingDate = asOf ?? today;
        string active = tab == "ledger" ? "ledger" : "aging";

        PayableLedgerResponse? ledger = null;
        if (active == "ledger" && vendorId is Guid vendor)
        {
            Result<PayableLedgerResponse> result = await ledgerQuery.Handle(
                new GetPayableLedgerQuery(vendor, start, end, branchFilter.BranchId), cancellationToken);
            ledger = result.IsSuccess ? result.Value : null;
        }

        PayableAgingResponse? aging = null;
        if (active == "aging")
        {
            Result<PayableAgingResponse> result = await agingQuery.Handle(
                new GetPayableAgingQuery(agingDate, branchFilter.BranchId, vendorId), cancellationToken);
            aging = result.IsSuccess ? result.Value : null;
        }

        return View(new PayablesViewModel
        {
            Tab = active,
            VendorId = vendorId,
            VendorLabel = await VendorLabelAsync(vendorId, cancellationToken),
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
        string? format, Guid vendorId, string? branch, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch ?? PageSupport.AllBranches);
        Result<PayableLedgerResponse> result = await ledgerQuery.Handle(
            new GetPayableLedgerQuery(vendorId, from, to, branchFilter.BranchId), cancellationToken);

        if (result.IsFailure)
        {
            NotifyError(result.Error.Description);
            return RedirectToAction(nameof(Index), new { tab = "ledger", vendorId, branch, from, to });
        }

        PayableLedgerResponse ledger = result.Value;
        return await support.ExportAsync(format, "Payable Ledger", "payable-ledger",
            [
                $"Vendor: {ledger.VendorCode} — {ledger.VendorName}",
                branchFilter.Description,
                $"Period: {formatter.Date(from)} – {formatter.Date(to)}",
                $"Opening: Rp {formatter.Number(ledger.OpeningBalance)} · Closing: Rp {formatter.Number(ledger.ClosingBalance)}"
            ],
            LedgerColumns, ledger.Lines);
    }

    [HttpGet]
    [MenuAccess(MenuCode, MenuRights.Export)]
    public async Task<IActionResult> AgingExport(
        string? format, Guid? vendorId, string? branch, DateOnly asOf, CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch ?? PageSupport.AllBranches);
        Result<PayableAgingResponse> result = await agingQuery.Handle(new GetPayableAgingQuery(asOf, branchFilter.BranchId, vendorId), cancellationToken);

        AgingRow[] rows = result.IsFailure
            ? []
            : [.. result.Value.Vendors.SelectMany(v => v.Invoices.Select(i => new AgingRow(
                $"{v.VendorCode} — {v.VendorName}", i.Number, i.DueDate, i.DaysOverdue, AgingBuckets.For(i.DaysOverdue, i.Outstanding))))];

        return await support.ExportAsync(format, "Payable Aging", "payable-aging",
            [branchFilter.Description, $"As of: {formatter.Date(asOf)}"], AgingColumns, rows);
    }

    private async Task<string?> VendorLabelAsync(Guid? vendorId, CancellationToken cancellationToken)
    {
        if (vendorId is not Guid id)
        {
            return null;
        }

        Result<VendorResponse> vendor = await vendorQuery.Handle(new GetVendorByIdQuery(id), cancellationToken);
        return vendor.IsSuccess ? $"{vendor.Value.Code} — {vendor.Value.Name}" : null;
    }

    private sealed record AgingRow(string Vendor, string Invoice, DateOnly DueDate, int DaysOverdue, AgingBuckets Buckets);
}
