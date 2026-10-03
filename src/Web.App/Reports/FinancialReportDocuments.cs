using Application.Finance.Reports;
using Application.Finance.Tax;
using Web.App.Infrastructure.Export;

namespace Web.App.Reports;

/// <summary>
/// Turns the report use cases' responses into <see cref="ReportDocument"/>s: the same structure is shown on screen and
/// exported to Excel and PDF (PLAN-WEBAPP §21.2).
/// </summary>
public static class FinancialReportDocuments
{
    private static readonly ReportColumn AccountColumn = new("Account", Width: 4);
    private static readonly ReportColumn AmountColumn = new("Amount", ExportFormat.Money, 1.6f);

    /// <param name="journalLink">Detail page of a journal, linked from each ledger line on screen.</param>
    public static ReportDocument GeneralLedger(GeneralLedgerResponse ledger, Func<Guid, string?> journalLink)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(journalLink);

        List<ReportRow> rows = [ReportRow.Subtotal("Opening balance", null, null, null, null, null, null, ledger.OpeningBalance)];
        rows.AddRange(ledger.Lines.Select(l => ReportRow.Detail(
            l.Number, l.Date, l.BranchCode, l.LineDescription ?? l.Description, l.CostCenterCode,
            l.Debit == 0 ? null : l.Debit, l.Credit == 0 ? null : l.Credit, l.Balance) with { Link = journalLink(l.JournalId) }));
        rows.Add(ReportRow.Subtotal("Total mutation", null, null, null, null, ledger.TotalDebit, ledger.TotalCredit, null));
        rows.Add(ReportRow.Total("Closing balance", null, null, null, null, null, null, ledger.ClosingBalance));

        return new ReportDocument(
        [
            new ReportTable(
                $"{ledger.AccountCode} — {ledger.AccountName}",
                [
                    new("Journal", Width: 1.6f), new("Date", ExportFormat.Date, 0.9f), new("Branch", Width: 0.7f),
                    new("Description", Width: 3.4f), new("Cost center", Width: 0.9f), new("Debit", ExportFormat.Money, 1.4f),
                    new("Credit", ExportFormat.Money, 1.4f), new("Balance", ExportFormat.Money, 1.5f)
                ],
                rows,
                [$"Balances in the account's normal direction ({ledger.NormalBalance}); positive = normal."])
        ], Landscape: true);
    }

    public static ReportDocument TrialBalance(TrialBalanceResponse trialBalance)
    {
        ArgumentNullException.ThrowIfNull(trialBalance);

        TrialBalanceTotals t = trialBalance.Totals;
        List<ReportRow> rows =
        [
            .. trialBalance.Lines.Select(l => ReportRow.Detail(
                l.AccountCode, l.AccountName, l.AccountType, l.OpeningDebit, l.OpeningCredit, l.MutationDebit, l.MutationCredit,
                l.ClosingDebit, l.ClosingCredit)),
            ReportRow.Total("Total", null, null, t.OpeningDebit, t.OpeningCredit, t.MutationDebit, t.MutationCredit, t.ClosingDebit, t.ClosingCredit)
        ];

        return new ReportDocument(
        [
            new ReportTable(
                null,
                [
                    new("Code", Width: 1), new("Account", Width: 3), new("Type", Width: 0.9f),
                    new("Opening debit", ExportFormat.Money, 1.4f), new("Opening credit", ExportFormat.Money, 1.4f),
                    new("Debit", ExportFormat.Money, 1.4f), new("Credit", ExportFormat.Money, 1.4f),
                    new("Closing debit", ExportFormat.Money, 1.4f), new("Closing credit", ExportFormat.Money, 1.4f)
                ],
                rows,
                [t.IsBalanced ? "The trial balance is balanced." : "WARNING: the trial balance is NOT balanced."])
        ], Landscape: true);
    }

    public static ReportDocument IncomeStatement(IncomeStatementResponse statement)
    {
        ArgumentNullException.ThrowIfNull(statement);

        List<ReportRow> rows = [];
        AddSections(rows, statement.Sections.Where(s => s.Type == "Revenue"), "Total revenue", statement.TotalRevenue);
        AddSections(rows, statement.Sections.Where(s => s.Type == "Expense"), "Total expenses", statement.TotalExpense);
        rows.Add(ReportRow.Total(statement.NetIncome >= 0 ? "Net income" : "Net loss", statement.NetIncome));

        return new ReportDocument([new ReportTable(null, [AccountColumn, AmountColumn], rows)]);
    }

    public static ReportDocument BalanceSheet(BalanceSheetResponse sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        List<ReportRow> rows = [];
        AddSections(rows, sheet.Assets, "Total assets", sheet.TotalAssets);
        AddSections(rows, sheet.Liabilities, "Total liabilities", sheet.TotalLiabilities);

        if (sheet.Equity.Count == 0)
        {
            rows.Add(ReportRow.Section("Equity"));
        }

        foreach (StatementSection section in sheet.Equity)
        {
            AddSection(rows, section);
        }

        rows.Add(ReportRow.Indented(1, "Current year earnings", sheet.CurrentYearEarnings));
        if (sheet.UnclosedPriorYearsEarnings != 0)
        {
            rows.Add(ReportRow.Indented(1, "Earnings of prior years not closed yet", sheet.UnclosedPriorYearsEarnings));
        }

        rows.Add(ReportRow.Subtotal("Total equity", sheet.TotalEquity));
        rows.Add(ReportRow.Total("Total liabilities and equity", sheet.TotalLiabilities + sheet.TotalEquity));

        return new ReportDocument(
        [
            new ReportTable(null, [AccountColumn, AmountColumn], rows,
                [sheet.IsBalanced ? "Assets equal liabilities + equity." : "WARNING: the balance sheet is NOT balanced."])
        ]);
    }

    public static ReportDocument CashFlow(CashFlowStatementResponse statement)
    {
        ArgumentNullException.ThrowIfNull(statement);

        List<ReportRow> rows = [ReportRow.Subtotal("Opening cash and bank", null, null, statement.OpeningCash)];
        foreach (CashFlowSection section in statement.Sections)
        {
            rows.Add(ReportRow.Section($"{section.Category} activities"));
            rows.AddRange(section.Lines.Select(l => ReportRow.Indented(1, $"{l.AccountCode} — {l.AccountName}", l.Inflow, l.Outflow, l.Net)));
            rows.Add(ReportRow.Subtotal($"Net cash from {section.Category} activities", section.Inflow, section.Outflow, section.Net));
        }

        rows.Add(ReportRow.Subtotal("Net change in cash", null, null, statement.NetChange));
        rows.Add(ReportRow.Total("Closing cash and bank", null, null, statement.ClosingCash));

        return new ReportDocument(
        [
            new ReportTable(
                null,
                [new("Counter account", Width: 4), new("Inflow", ExportFormat.Money, 1.5f), new("Outflow", ExportFormat.Money, 1.5f), new("Net", ExportFormat.Money, 1.5f)],
                rows,
                [statement.IsConsistent
                    ? "The closing cash equals the cash/bank account balances."
                    : $"WARNING: the cash/bank account balances are {statement.ClosingCashPerLedger:N2}."])
        ]);
    }

    public static ReportDocument Profitability(ProfitabilityResponse report)
    {
        ArgumentNullException.ThrowIfNull(report);

        bool branch = report.GroupBy == nameof(ProfitabilityGrouping.Branch);
        List<ReportColumn> columns =
        [
            new("Code", Width: 1.4f), new("Name", Width: 2.2f), new("Cycles", ExportFormat.WholeNumber, 0.7f),
            new("Birds", ExportFormat.WholeNumber, 0.9f), new("Kg", ExportFormat.Number, 1), new("Net sales", ExportFormat.Money, 1.5f),
            new("HPP", ExportFormat.Money, 1.5f), new("Plasma fee", ExportFormat.Money, 1.4f), new("Margin", ExportFormat.Money, 1.5f),
            new("Margin/kg", ExportFormat.Money, 1)
        ];
        if (branch)
        {
            columns.Add(new("Ledger net income", ExportFormat.Money, 1.5f));
            columns.Add(new("Overhead & other", ExportFormat.Money, 1.5f));
        }

        object?[] Cells(ProfitabilityRow r) =>
        [
            r.Code, r.Name, r.Cycles, r.Birds, r.WeightKg, r.NetSales, r.CostOfGoodsSold, r.PlasmaFee, r.Margin, r.MarginPerKg,
            .. branch ? new object?[] { r.LedgerNetIncome, r.OverheadAndOther } : []
        ];

        List<ReportRow> rows = [.. report.Rows.Select(r => ReportRow.Detail(Cells(r)))];
        rows.Add(new ReportRow(ReportRowKind.Total, Cells(report.Total with { Code = "Total", Name = string.Empty })));

        return new ReportDocument(
        [
            new ReportTable(null, columns, rows,
                ["Contribution margin = net sales (excl. VAT, after credit notes) − HPP − plasma fee; overhead is not allocated."])
        ], Landscape: true);
    }

    public static ReportDocument Vat(VatRecapResponse recap)
    {
        ArgumentNullException.ThrowIfNull(recap);

        ReportTable output = new(
            "PPN keluaran",
            [
                new("Invoice", Width: 1.6f), new("Date", ExportFormat.Date, 0.9f), new("Branch", Width: 0.7f), new("Customer", Width: 2.2f),
                new("NPWP", Width: 1.3f), new("NITKU", Width: 1.3f), new("PKP", ExportFormat.Boolean, 0.5f), new("Tax codes", Width: 1),
                new("DPP", ExportFormat.Money, 1.4f), new("DPP nilai lain", ExportFormat.Money, 1.4f), new("PPN", ExportFormat.Money, 1.3f)
            ],
            [
                .. recap.Output.Select(l => ReportRow.Detail(
                    l.InvoiceNumber, l.InvoiceDate, l.BranchCode, $"{l.CustomerCode} — {l.CustomerName}", l.Npwp, l.Nitku, l.IsPkp, l.TaxCodes,
                    l.Dpp, l.DppNilaiLain, l.Ppn)),
                ReportRow.Total("Total", null, null, null, null, null, null, null,
                    recap.Output.Sum(l => l.Dpp), recap.Output.Sum(l => l.DppNilaiLain), recap.TotalOutputVat)
            ]);

        ReportTable returns = new(
            "Retur PPN keluaran",
            [
                new("Credit note", Width: 1.6f), new("Date", ExportFormat.Date, 0.9f), new("Invoice", Width: 1.6f), new("Customer", Width: 2.2f),
                new("NPWP", Width: 1.3f), new("DPP", ExportFormat.Money, 1.4f), new("PPN", ExportFormat.Money, 1.3f)
            ],
            [
                .. recap.OutputReturns.Select(l => ReportRow.Detail(l.CreditNoteNumber, l.Date, l.InvoiceNumber, l.CustomerName, l.Npwp, l.Dpp, l.Ppn)),
                ReportRow.Total("Total", null, null, null, null, recap.OutputReturns.Sum(l => l.Dpp), recap.TotalOutputVatReturns)
            ]);

        ReportTable input = new(
            "PPN masukan",
            [
                new("Internal no.", Width: 1.5f), new("Vendor invoice", Width: 1.3f), new("Faktur pajak", Width: 1.4f),
                new("Date", ExportFormat.Date, 0.9f), new("Branch", Width: 0.7f), new("Vendor", Width: 2.1f), new("NPWP", Width: 1.3f),
                new("NITKU", Width: 1.2f), new("DPP", ExportFormat.Money, 1.4f), new("DPP nilai lain", ExportFormat.Money, 1.4f),
                new("PPN", ExportFormat.Money, 1.3f)
            ],
            [
                .. recap.Input.Select(l => ReportRow.Detail(
                    l.InvoiceNumber, l.VendorInvoiceNumber, l.TaxInvoiceNumber, l.InvoiceDate, l.BranchCode, $"{l.VendorCode} — {l.VendorName}",
                    l.Npwp, l.Nitku, l.Dpp, l.DppNilaiLain, l.Ppn)),
                ReportRow.Total("Total", null, null, null, null, null, null, null,
                    recap.Input.Sum(l => l.Dpp), recap.Input.Sum(l => l.DppNilaiLain), recap.TotalInputVat)
            ]);

        ReportTable summary = new(
            "Ringkasan PPN",
            [new("Item", Width: 4), AmountColumn],
            [
                ReportRow.Detail("PPN keluaran", recap.TotalOutputVat),
                ReportRow.Detail("Retur PPN keluaran", -recap.TotalOutputVatReturns),
                ReportRow.Detail("PPN masukan", -recap.TotalInputVat),
                ReportRow.Total(recap.NetVat >= 0 ? "PPN kurang bayar" : "PPN lebih bayar", recap.NetVat)
            ]);

        return new ReportDocument([summary, output, returns, input], Landscape: true);
    }

    public static ReportDocument Withholding(WithholdingRecapResponse recap)
    {
        ArgumentNullException.ThrowIfNull(recap);

        ReportTable lines = new(
            "PPh dipotong",
            [
                new("Source", Width: 1.2f), new("Document", Width: 1.6f), new("Date", ExportFormat.Date, 0.9f), new("Branch", Width: 0.7f),
                new("Payee", Width: 2.2f), new("NPWP", Width: 1.3f), new("NIK", Width: 1.3f), new("Tax code", Width: 1),
                new("Article", Width: 0.8f), new("Tax base", ExportFormat.Money, 1.4f), new("Rate %", ExportFormat.Number, 0.6f),
                new("PPh", ExportFormat.Money, 1.3f)
            ],
            [
                .. recap.Lines.Select(l => ReportRow.Detail(
                    l.Source == "PlasmaSettlement" ? "Plasma settlement" : "Vendor invoice", l.DocumentNumber, l.Date, l.BranchCode,
                    $"{l.PayeeCode} — {l.PayeeName}", l.Npwp, l.Nik, l.TaxCode, l.Article, l.TaxBase, l.RatePercent, l.Amount)),
                ReportRow.Total("Total", null, null, null, null, null, null, null, null, recap.Lines.Sum(l => l.TaxBase), null, recap.Total)
            ]);

        ReportTable totals = new(
            "PPh per tax code",
            [new("Tax code", Width: 1.5f), new("Article", Width: 1), new("Tax base", ExportFormat.Money, 1.6f), new("PPh", ExportFormat.Money, 1.6f)],
            [
                .. recap.TotalsPerTaxCode.Select(t => ReportRow.Detail(t.TaxCode, t.Article, t.TaxBase, t.Amount)),
                ReportRow.Total("Total", null, recap.TotalsPerTaxCode.Sum(t => t.TaxBase), recap.Total)
            ]);

        return new ReportDocument([totals, lines], Landscape: true);
    }

    /// <summary>
    /// Sections with their accounts; a subtotal per section only when there are several (one section's subtotal would
    /// repeat the total).
    /// </summary>
    private static void AddSections(List<ReportRow> rows, IEnumerable<StatementSection> sections, string totalLabel, decimal total)
    {
        List<StatementSection> list = [.. sections];
        foreach (StatementSection section in list)
        {
            AddSection(rows, section);
            if (list.Count > 1)
            {
                rows.Add(ReportRow.Subtotal($"Total {section.Name}", section.Total));
            }
        }

        rows.Add(ReportRow.Total(totalLabel, total));
    }

    private static void AddSection(List<ReportRow> rows, StatementSection section)
    {
        rows.Add(ReportRow.Section($"{section.Code} {section.Name}"));
        rows.AddRange(section.Accounts.Select(a => ReportRow.Indented(1, $"{a.Code} — {a.Name}", a.Amount)));
    }
}
