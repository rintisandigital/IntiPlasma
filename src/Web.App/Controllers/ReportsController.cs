using System.Globalization;
using Application.Abstractions.Messaging;
using Application.Finance.Reports;
using Application.Finance.Tax;
using Domain.Access;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SharedKernel;
using Web.App.Areas.MasterData.Models;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Formatting;
using Web.App.Reports;

namespace Web.App.Controllers;

/// <summary>
/// Financial reports (PLAN-WEBAPP §21.2): each page has a parameter form (query string), shows the report as a
/// structured table and exports the same <see cref="ReportDocument"/> to Excel or PDF; the tax recap also offers the
/// CSV files of Web.Api.
/// </summary>
public sealed class ReportsController(PageSupport support, DisplayFormatter formatter) : AppController
{
    private delegate Task<Result<(ReportDocument Report, List<string> Filters)>> ReportRunner(CancellationToken cancellationToken);

    // ---- General ledger ------------------------------------------------------------------------------------------

    [HttpGet]
    [MenuAccess(MenuCodes.ReportsGeneralLedger)]
    public async Task<IActionResult> GeneralLedger(
        Guid? accountId, DateOnly? from, DateOnly? to, string? branch, Guid? costCenterId,
        [FromServices] IQueryHandler<GetGeneralLedgerQuery, GeneralLedgerResponse> query,
        [FromServices] IQueryHandler<Application.Finance.CostCenters.GetCostCentersQuery, IReadOnlyList<Application.Finance.CostCenters.CostCenterResponse>> costCenters,
        CancellationToken cancellationToken)
    {
        (DateOnly start, DateOnly end) = Period(from, to);
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        string? accountLabel = null;

        ReportRunner? runner = accountId is Guid account
            ? async ct =>
            {
                Result<GeneralLedgerResponse> result = await query.Handle(new GetGeneralLedgerQuery(account, start, end, branchFilter.BranchId, costCenterId), ct);
                if (result.IsFailure)
                {
                    return Result.Failure<(ReportDocument, List<string>)>(result.Error);
                }

                accountLabel = $"{result.Value.AccountCode} — {result.Value.AccountName}";
                return (FinancialReportDocuments.GeneralLedger(result.Value, JournalLink), PeriodFilters(branchFilter, start, end));
            }
            : null;

        IReadOnlyList<SelectListItem> costCenterOptions = await Areas.Finance.FinanceOptions.CostCentersAsync(costCenters, [costCenterId], cancellationToken);
        ReportPageViewModel page = await PageAsync("General Ledger", "Mutations and running balance of one account", MenuCodes.ReportsGeneralLedger,
            nameof(GeneralLedgerExport), runner, cancellationToken);

        return View("Report", page with
        {
            Fields =
            [
                new ReportField("accountId", "Account", ReportFieldKind.Lookup, accountId?.ToString(),
                    LookupUrl: Url.Action("Accounts", "Lookup"), LookupLabel: accountLabel, Required: true, Width: 3),
                DateField("from", "From", start),
                DateField("to", "To", end),
                BranchField(branchFilter),
                new ReportField("costCenterId", "Cost center", ReportFieldKind.Select, costCenterId?.ToString(), costCenterOptions)
            ]
        });
    }

    [HttpGet]
    [MenuAccess(MenuCodes.ReportsGeneralLedger, MenuRights.Export)]
    public async Task<IActionResult> GeneralLedgerExport(
        string? format, Guid accountId, DateOnly? from, DateOnly? to, string? branch, Guid? costCenterId,
        [FromServices] IQueryHandler<GetGeneralLedgerQuery, GeneralLedgerResponse> query,
        CancellationToken cancellationToken)
    {
        (DateOnly start, DateOnly end) = Period(from, to);
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        Result<GeneralLedgerResponse> result = await query.Handle(new GetGeneralLedgerQuery(accountId, start, end, branchFilter.BranchId, costCenterId), cancellationToken);

        return await ExportAsync(format, "General Ledger", result.IsSuccess ? $"general-ledger-{result.Value.AccountCode}" : "general-ledger", result,
            r => FinancialReportDocuments.GeneralLedger(r, _ => null), PeriodFilters(branchFilter, start, end));
    }

    // ---- Trial balance ---------------------------------------------------------------------------------------------

    [HttpGet]
    [MenuAccess(MenuCodes.ReportsTrialBalance)]
    public async Task<IActionResult> TrialBalance(
        DateOnly? from, DateOnly? to, string? branch, bool? zero,
        [FromServices] IQueryHandler<GetTrialBalanceQuery, TrialBalanceResponse> query,
        CancellationToken cancellationToken)
    {
        (DateOnly start, DateOnly end) = Period(from, to);
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        ReportPageViewModel page = await PageAsync("Trial Balance", "Opening balance, mutations and closing balance per account", MenuCodes.ReportsTrialBalance,
            nameof(TrialBalanceExport),
            async ct => Map(await query.Handle(new GetTrialBalanceQuery(start, end, branchFilter.BranchId, zero == true), ct),
                FinancialReportDocuments.TrialBalance, PeriodFilters(branchFilter, start, end)),
            cancellationToken);

        return View("Report", page with
        {
            Fields = [DateField("from", "From", start), DateField("to", "To", end), BranchField(branchFilter),
                new ReportField("zero", "Include zero balances", ReportFieldKind.Checkbox, zero == true ? "true" : null)]
        });
    }

    [HttpGet]
    [MenuAccess(MenuCodes.ReportsTrialBalance, MenuRights.Export)]
    public async Task<IActionResult> TrialBalanceExport(
        string? format, DateOnly? from, DateOnly? to, string? branch, bool? zero,
        [FromServices] IQueryHandler<GetTrialBalanceQuery, TrialBalanceResponse> query,
        CancellationToken cancellationToken)
    {
        (DateOnly start, DateOnly end) = Period(from, to);
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await ExportAsync(format, "Trial Balance", "trial-balance",
            await query.Handle(new GetTrialBalanceQuery(start, end, branchFilter.BranchId, zero == true), cancellationToken),
            FinancialReportDocuments.TrialBalance, PeriodFilters(branchFilter, start, end));
    }

    // ---- Income statement ------------------------------------------------------------------------------------------

    [HttpGet]
    [MenuAccess(MenuCodes.ReportsIncomeStatement)]
    public async Task<IActionResult> IncomeStatement(
        DateOnly? from, DateOnly? to, string? branch,
        [FromServices] IQueryHandler<GetIncomeStatementQuery, IncomeStatementResponse> query,
        CancellationToken cancellationToken)
    {
        (DateOnly start, DateOnly end) = Period(from, to);
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        ReportPageViewModel page = await PageAsync("Income Statement", "Revenue and expenses of the period (laba rugi)", MenuCodes.ReportsIncomeStatement,
            nameof(IncomeStatementExport),
            async ct => Map(await query.Handle(new GetIncomeStatementQuery(start, end, branchFilter.BranchId), ct),
                FinancialReportDocuments.IncomeStatement, PeriodFilters(branchFilter, start, end)),
            cancellationToken);

        return View("Report", page with { Fields = [DateField("from", "From", start), DateField("to", "To", end), BranchField(branchFilter)] });
    }

    [HttpGet]
    [MenuAccess(MenuCodes.ReportsIncomeStatement, MenuRights.Export)]
    public async Task<IActionResult> IncomeStatementExport(
        string? format, DateOnly? from, DateOnly? to, string? branch,
        [FromServices] IQueryHandler<GetIncomeStatementQuery, IncomeStatementResponse> query,
        CancellationToken cancellationToken)
    {
        (DateOnly start, DateOnly end) = Period(from, to);
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await ExportAsync(format, "Income Statement", "income-statement",
            await query.Handle(new GetIncomeStatementQuery(start, end, branchFilter.BranchId), cancellationToken),
            FinancialReportDocuments.IncomeStatement, PeriodFilters(branchFilter, start, end));
    }

    // ---- Balance sheet ---------------------------------------------------------------------------------------------

    [HttpGet]
    [MenuAccess(MenuCodes.ReportsBalanceSheet)]
    public async Task<IActionResult> BalanceSheet(
        DateOnly? asOf, string? branch,
        [FromServices] IQueryHandler<GetBalanceSheetQuery, BalanceSheetResponse> query,
        CancellationToken cancellationToken)
    {
        DateOnly date = asOf ?? formatter.Today();
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        ReportPageViewModel page = await PageAsync("Balance Sheet", "Assets, liabilities and equity as of a date (neraca)", MenuCodes.ReportsBalanceSheet,
            nameof(BalanceSheetExport),
            async ct => Map(await query.Handle(new GetBalanceSheetQuery(date, branchFilter.BranchId), ct),
                FinancialReportDocuments.BalanceSheet, AsOfFilters(branchFilter, date)),
            cancellationToken);

        return View("Report", page with { Fields = [DateField("asOf", "As of", date), BranchField(branchFilter)] });
    }

    [HttpGet]
    [MenuAccess(MenuCodes.ReportsBalanceSheet, MenuRights.Export)]
    public async Task<IActionResult> BalanceSheetExport(
        string? format, DateOnly? asOf, string? branch,
        [FromServices] IQueryHandler<GetBalanceSheetQuery, BalanceSheetResponse> query,
        CancellationToken cancellationToken)
    {
        DateOnly date = asOf ?? formatter.Today();
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await ExportAsync(format, "Balance Sheet", "balance-sheet",
            await query.Handle(new GetBalanceSheetQuery(date, branchFilter.BranchId), cancellationToken),
            FinancialReportDocuments.BalanceSheet, AsOfFilters(branchFilter, date));
    }

    // ---- Cash flow -------------------------------------------------------------------------------------------------

    [HttpGet]
    [MenuAccess(MenuCodes.ReportsCashFlow)]
    public async Task<IActionResult> CashFlow(
        DateOnly? from, DateOnly? to, string? branch,
        [FromServices] IQueryHandler<GetCashFlowStatementQuery, CashFlowStatementResponse> query,
        CancellationToken cancellationToken)
    {
        (DateOnly start, DateOnly end) = Period(from, to);
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        ReportPageViewModel page = await PageAsync("Cash Flow", "Cash and bank movements by activity (direct method)", MenuCodes.ReportsCashFlow,
            nameof(CashFlowExport),
            async ct => Map(await query.Handle(new GetCashFlowStatementQuery(start, end, branchFilter.BranchId), ct),
                FinancialReportDocuments.CashFlow, PeriodFilters(branchFilter, start, end)),
            cancellationToken);

        return View("Report", page with { Fields = [DateField("from", "From", start), DateField("to", "To", end), BranchField(branchFilter)] });
    }

    [HttpGet]
    [MenuAccess(MenuCodes.ReportsCashFlow, MenuRights.Export)]
    public async Task<IActionResult> CashFlowExport(
        string? format, DateOnly? from, DateOnly? to, string? branch,
        [FromServices] IQueryHandler<GetCashFlowStatementQuery, CashFlowStatementResponse> query,
        CancellationToken cancellationToken)
    {
        (DateOnly start, DateOnly end) = Period(from, to);
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);

        return await ExportAsync(format, "Cash Flow", "cash-flow",
            await query.Handle(new GetCashFlowStatementQuery(start, end, branchFilter.BranchId), cancellationToken),
            FinancialReportDocuments.CashFlow, PeriodFilters(branchFilter, start, end));
    }

    // ---- Profitability ---------------------------------------------------------------------------------------------

    [HttpGet]
    [MenuAccess(MenuCodes.ReportsProfitability)]
    public async Task<IActionResult> Profitability(
        DateOnly? from, DateOnly? to, ProfitabilityGrouping? groupBy, string? branch, Guid? farmerId, string? farmerLabel,
        [FromServices] IQueryHandler<GetProfitabilityQuery, ProfitabilityResponse> query,
        CancellationToken cancellationToken)
    {
        (DateOnly start, DateOnly end) = Period(from, to);
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        ProfitabilityGrouping grouping = groupBy ?? ProfitabilityGrouping.Cycle;

        ReportPageViewModel page = await PageAsync("Profitability", "Contribution margin per cycle, coop, farmer or branch", MenuCodes.ReportsProfitability,
            nameof(ProfitabilityExport),
            async ct => Map(await query.Handle(new GetProfitabilityQuery(start, end, grouping, branchFilter.BranchId, farmerId), ct),
                FinancialReportDocuments.Profitability, ProfitabilityFilters(branchFilter, start, end, grouping)),
            cancellationToken);

        return View("Report", page with
        {
            Fields =
            [
                DateField("from", "From", start), DateField("to", "To", end),
                new ReportField("groupBy", "Group by", ReportFieldKind.Select, grouping.ToString(), EnumOptions.For<ProfitabilityGrouping>(grouping)),
                BranchField(branchFilter),
                new ReportField("farmerId", "Farmer", ReportFieldKind.Lookup, farmerId?.ToString(), LookupUrl: Url.Action("Farmers", "Lookup"),
                    LookupLabel: farmerLabel, Width: 3)
            ]
        });
    }

    [HttpGet]
    [MenuAccess(MenuCodes.ReportsProfitability, MenuRights.Export)]
    public async Task<IActionResult> ProfitabilityExport(
        string? format, DateOnly? from, DateOnly? to, ProfitabilityGrouping? groupBy, string? branch, Guid? farmerId,
        [FromServices] IQueryHandler<GetProfitabilityQuery, ProfitabilityResponse> query,
        CancellationToken cancellationToken)
    {
        (DateOnly start, DateOnly end) = Period(from, to);
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        ProfitabilityGrouping grouping = groupBy ?? ProfitabilityGrouping.Cycle;

        return await ExportAsync(format, "Profitability", "profitability",
            await query.Handle(new GetProfitabilityQuery(start, end, grouping, branchFilter.BranchId, farmerId), cancellationToken),
            FinancialReportDocuments.Profitability, ProfitabilityFilters(branchFilter, start, end, grouping));
    }

    // ---- Tax recap -------------------------------------------------------------------------------------------------

    /// <param name="tab">"vat" (PPN) or "withholding" (PPh dipotong).</param>
    [HttpGet]
    [MenuAccess(MenuCodes.ReportsTax)]
    public async Task<IActionResult> Tax(
        int? year, int? month, string? branch, string? tab,
        [FromServices] IQueryHandler<GetVatRecapQuery, VatRecapResponse> vatQuery,
        [FromServices] IQueryHandler<GetWithholdingRecapQuery, WithholdingRecapResponse> withholdingQuery,
        CancellationToken cancellationToken)
    {
        DateOnly today = formatter.Today();
        int y = year ?? today.Year;
        int m = month ?? today.Month;
        bool withholding = tab == "withholding";
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        List<string> filters = TaxFilters(branchFilter, y, m, withholding);

        ReportPageViewModel page = await PageAsync("Tax Recap", "PPN (output, returns, input) and PPh withheld per tax period", MenuCodes.ReportsTax,
            nameof(TaxExport),
            async ct => withholding
                ? Map(await withholdingQuery.Handle(new GetWithholdingRecapQuery(y, m, branchFilter.BranchId), ct), FinancialReportDocuments.Withholding, filters)
                : Map(await vatQuery.Handle(new GetVatRecapQuery(y, m, branchFilter.BranchId), ct), FinancialReportDocuments.Vat, filters),
            cancellationToken);

        bool canExport = await support.CanAsync(MenuCodes.ReportsTax, MenuRights.Export);
        string? branchValue = branchFilter.BranchId?.ToString() ?? PageSupport.AllBranches;
        string Csv(string section) => Url.Action(nameof(TaxCsv), new { section, year = y, month = m, branch = branchValue })!;
        List<(string Label, string Url)> csvLinks = withholding
            ? [("CSV PPh dipotong", Csv("withholding"))]
            : [("CSV PPN keluaran", Csv("output")), ("CSV retur", Csv("output-returns")), ("CSV PPN masukan", Csv("input"))];

        return View("Report", page with
        {
            Fields =
            [
                new ReportField("tab", "Recap", ReportFieldKind.Select, withholding ? "withholding" : "vat",
                    [new SelectListItem("PPN (VAT)", "vat", !withholding), new SelectListItem("PPh withheld", "withholding", withholding)]),
                new ReportField("year", "Year", ReportFieldKind.Number, y.ToString(CultureInfo.InvariantCulture), Width: 1),
                new ReportField("month", "Month", ReportFieldKind.Select, m.ToString(CultureInfo.InvariantCulture), Months(m)),
                BranchField(branchFilter)
            ],
            CsvLinks = canExport ? csvLinks : null
        });
    }

    [HttpGet]
    [MenuAccess(MenuCodes.ReportsTax, MenuRights.Export)]
    public async Task<IActionResult> TaxExport(
        string? format, int? year, int? month, string? branch, string? tab,
        [FromServices] IQueryHandler<GetVatRecapQuery, VatRecapResponse> vatQuery,
        [FromServices] IQueryHandler<GetWithholdingRecapQuery, WithholdingRecapResponse> withholdingQuery,
        CancellationToken cancellationToken)
    {
        DateOnly today = formatter.Today();
        int y = year ?? today.Year;
        int m = month ?? today.Month;
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        string period = $"{y}{m:D2}";

        return tab == "withholding"
            ? await ExportAsync(format, "PPh Withheld", $"pph-{period}",
                await withholdingQuery.Handle(new GetWithholdingRecapQuery(y, m, branchFilter.BranchId), cancellationToken),
                FinancialReportDocuments.Withholding, TaxFilters(branchFilter, y, m, true))
            : await ExportAsync(format, "PPN Recap", $"ppn-{period}",
                await vatQuery.Handle(new GetVatRecapQuery(y, m, branchFilter.BranchId), cancellationToken),
                FinancialReportDocuments.Vat, TaxFilters(branchFilter, y, m, false));
    }

    /// <summary>
    /// The CSV files of the tax recap, with the same columns as Web.Api (<c>finance/tax/…/export</c>).
    /// </summary>
    /// <param name="section">output, output-returns, input (PPN) or withholding (PPh).</param>
    [HttpGet]
    [MenuAccess(MenuCodes.ReportsTax, MenuRights.Export)]
    public async Task<IActionResult> TaxCsv(
        string section, int year, int month, string? branch,
        [FromServices] IQueryHandler<GetVatRecapQuery, VatRecapResponse> vatQuery,
        [FromServices] IQueryHandler<GetWithholdingRecapQuery, WithholdingRecapResponse> withholdingQuery,
        CancellationToken cancellationToken)
    {
        BranchFilter branchFilter = await support.BranchFilterAsync(branch);
        string period = $"{year}{month:D2}";
        ExportService exports = support.Exports;

        if (section == "withholding")
        {
            Result<WithholdingRecapResponse> pph = await withholdingQuery.Handle(new GetWithholdingRecapQuery(year, month, branchFilter.BranchId), cancellationToken);
            return pph.IsFailure
                ? BadRequest(pph.Error.Description)
                : exports.Csv("PPh dipotong (CSV)", $"pph-dipotong-{period}.csv", pph.Value.Lines,
                    ("sumber", l => l.Source), ("nomor_dokumen", l => l.DocumentNumber), ("tanggal", l => l.Date),
                    ("cabang", l => l.BranchCode), ("kode_penerima", l => l.PayeeCode), ("nama_penerima", l => l.PayeeName),
                    ("npwp", l => l.Npwp), ("nik", l => l.Nik), ("kode_pajak", l => l.TaxCode), ("pasal", l => l.Article),
                    ("dasar_pengenaan", l => l.TaxBase), ("tarif_persen", l => l.RatePercent), ("pph", l => l.Amount));
        }

        Result<VatRecapResponse> vat = await vatQuery.Handle(new GetVatRecapQuery(year, month, branchFilter.BranchId), cancellationToken);
        if (vat.IsFailure)
        {
            return BadRequest(vat.Error.Description);
        }

        VatRecapResponse r = vat.Value;
        return section switch
        {
            "output" => exports.Csv("PPN keluaran (CSV)", $"ppn-keluaran-{period}.csv", r.Output,
                ("nomor_invoice", l => l.InvoiceNumber), ("tanggal", l => l.InvoiceDate), ("cabang", l => l.BranchCode),
                ("kode_customer", l => l.CustomerCode), ("nama_customer", l => l.CustomerName), ("npwp", l => l.Npwp),
                ("nitku", l => l.Nitku), ("pkp", l => l.IsPkp), ("kode_pajak", l => l.TaxCodes), ("dpp", l => l.Dpp),
                ("dpp_nilai_lain", l => l.DppNilaiLain), ("ppn", l => l.Ppn)),
            "output-returns" => exports.Csv("Retur PPN keluaran (CSV)", $"retur-ppn-keluaran-{period}.csv", r.OutputReturns,
                ("nomor_nota_kredit", l => l.CreditNoteNumber), ("tanggal", l => l.Date), ("nomor_invoice", l => l.InvoiceNumber),
                ("nama_customer", l => l.CustomerName), ("npwp", l => l.Npwp), ("dpp", l => l.Dpp), ("ppn", l => l.Ppn)),
            "input" => exports.Csv("PPN masukan (CSV)", $"ppn-masukan-{period}.csv", r.Input,
                ("nomor_internal", l => l.InvoiceNumber), ("nomor_invoice_vendor", l => l.VendorInvoiceNumber),
                ("nomor_faktur_pajak", l => l.TaxInvoiceNumber), ("tanggal", l => l.InvoiceDate), ("cabang", l => l.BranchCode),
                ("kode_vendor", l => l.VendorCode), ("nama_vendor", l => l.VendorName), ("npwp", l => l.Npwp),
                ("nitku", l => l.Nitku), ("dpp", l => l.Dpp), ("dpp_nilai_lain", l => l.DppNilaiLain), ("ppn", l => l.Ppn)),
            _ => BadRequest("section must be output, output-returns, input or withholding")
        };
    }

    // ---- Helpers ---------------------------------------------------------------------------------------------------

    private async Task<ReportPageViewModel> PageAsync(
        string title, string subtitle, string menuCode, string exportAction, ReportRunner? runner, CancellationToken cancellationToken)
    {
        ReportDocument? report = null;
        string? error = null;
        if (runner is not null)
        {
            Result<(ReportDocument Report, List<string> Filters)> result = await runner(cancellationToken);
            if (result.IsSuccess)
            {
                report = result.Value.Report;
            }
            else
            {
                error = result.Error.Description;
            }
        }

        return new ReportPageViewModel(title, subtitle, menuCode, [], report, error, exportAction);
    }

    private async Task<IActionResult> ExportAsync<T>(
        string? format, string title, string fileName, Result<T> result, Func<T, ReportDocument> build, IReadOnlyList<string> filters)
    {
        if (!ExportFileFormats.TryParse(format, out ExportFileFormat fileFormat))
        {
            return BadRequest("Unknown export format.");
        }

        if (result.IsFailure)
        {
            return result.Error.Type == ErrorType.Forbidden ? Forbid() : BadRequest(result.Error.Description);
        }

        ExportHeader header = await support.Exports.HeaderAsync(title, fileName, filters);
        return support.Exports.Report(fileFormat, header, build(result.Value));
    }

    private static Result<(ReportDocument Report, List<string> Filters)> Map<T>(Result<T> result, Func<T, ReportDocument> build, List<string> filters) =>
        result.IsSuccess
            ? (build(result.Value), filters)
            : Result.Failure<(ReportDocument, List<string>)>(result.Error);

    private string? JournalLink(Guid journalId) => Url.Action("Details", "Journals", new { area = "Finance", id = journalId });

    private (DateOnly From, DateOnly To) Period(DateOnly? from, DateOnly? to)
    {
        DateOnly today = formatter.Today();
        return (from ?? new DateOnly(today.Year, today.Month, 1), to ?? today);
    }

    private List<string> PeriodFilters(BranchFilter branchFilter, DateOnly from, DateOnly to) =>
        [branchFilter.Description, $"Period: {formatter.Date(from)} – {formatter.Date(to)}"];

    private List<string> AsOfFilters(BranchFilter branchFilter, DateOnly asOf) =>
        [branchFilter.Description, $"As of {formatter.Date(asOf)}"];

    private List<string> ProfitabilityFilters(BranchFilter branchFilter, DateOnly from, DateOnly to, ProfitabilityGrouping grouping) =>
        [.. PeriodFilters(branchFilter, from, to), $"Grouped by: {grouping}"];

    private static List<string> TaxFilters(BranchFilter branchFilter, int year, int month, bool withholding) =>
        [branchFilter.Description, $"Tax period (masa): {CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month)} {year}",
            withholding ? "PPh withheld" : "PPN"];

    private static ReportField DateField(string name, string label, DateOnly value) =>
        new(name, label, ReportFieldKind.Date, value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Required: true);

    private static ReportField BranchField(BranchFilter branchFilter) =>
        new("branch", "Branch", ReportFieldKind.Select, null, branchFilter.Options);

    private static IReadOnlyList<SelectListItem> Months(int selected) =>
        [.. Enumerable.Range(1, 12).Select(m => new SelectListItem(
            CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m), m.ToString(CultureInfo.InvariantCulture), m == selected))];
}
