using System.Data.Common;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using SharedKernel;

namespace Application.Finance.Tax;

/// <summary>
/// Rekap PPN per masa (month): PPN keluaran of posted sales invoices, its reduction by credit notes (retur), and PPN
/// masukan of posted vendor invoices; the difference is the VAT payable (positive) or overpaid (negative).
/// </summary>
public sealed record GetVatRecapQuery(int Year, int Month, Guid? BranchId) : IQuery<VatRecapResponse>;

/// <summary>
/// PPh withheld per masa: on vendor invoices and on plasma settlements (bases for the bukti potong).
/// </summary>
public sealed record GetWithholdingRecapQuery(int Year, int Month, Guid? BranchId) : IQuery<WithholdingRecapResponse>;

public sealed record OutputVatLine(
    string InvoiceNumber,
    DateOnly InvoiceDate,
    string BranchCode,
    string CustomerCode,
    string CustomerName,
    string? Npwp,
    string? Nitku,
    bool IsPkp,
    string? TaxCodes,
    decimal Dpp,
    decimal DppNilaiLain,
    decimal Ppn);

public sealed record OutputVatReturnLine(
    string CreditNoteNumber,
    DateOnly Date,
    string InvoiceNumber,
    string CustomerName,
    string? Npwp,
    decimal Dpp,
    decimal Ppn);

public sealed record InputVatLine(
    string InvoiceNumber,
    string VendorInvoiceNumber,
    string? TaxInvoiceNumber,
    DateOnly InvoiceDate,
    string BranchCode,
    string VendorCode,
    string VendorName,
    string? Npwp,
    string? Nitku,
    decimal Dpp,
    decimal DppNilaiLain,
    decimal Ppn);

/// <param name="NetVat">PPN keluaran − retur − PPN masukan: positive = kurang bayar, negative = lebih bayar.</param>
public sealed record VatRecapResponse(
    int Year,
    int Month,
    IReadOnlyList<OutputVatLine> Output,
    IReadOnlyList<OutputVatReturnLine> OutputReturns,
    IReadOnlyList<InputVatLine> Input,
    decimal TotalOutputVat,
    decimal TotalOutputVatReturns,
    decimal TotalInputVat,
    decimal NetVat);

/// <param name="Source">"VendorInvoice" or "PlasmaSettlement".</param>
public sealed record WithholdingLine(
    string Source,
    string DocumentNumber,
    DateOnly Date,
    string BranchCode,
    string PayeeCode,
    string PayeeName,
    string? Npwp,
    string? Nik,
    string TaxCode,
    string? Article,
    decimal TaxBase,
    decimal RatePercent,
    decimal Amount);

public sealed record WithholdingRecapResponse(
    int Year,
    int Month,
    IReadOnlyList<WithholdingLine> Lines,
    IReadOnlyList<WithholdingTotal> TotalsPerTaxCode,
    decimal Total);

public sealed record WithholdingTotal(string TaxCode, string? Article, decimal TaxBase, decimal Amount);


internal static class TaxSql
{
    public static (DateOnly Start, DateOnly End) Period(int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        return (start, start.AddMonths(1).AddDays(-1));
    }

    public static string Branch(string alias) =>
        $"""
        (@AllBranches OR {alias}.branch_id = ANY(@BranchIds))
        AND (@BranchId::uuid IS NULL OR {alias}.branch_id = @BranchId)
        """;
}


internal sealed class GetVatRecapQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetVatRecapQuery, VatRecapResponse>
{
    public async Task<Result<VatRecapResponse>> Handle(GetVatRecapQuery query, CancellationToken cancellationToken)
    {
        if (query.Month is < 1 or > 12 || query.Year is < 2000 or > 2100)
        {
            return Result.Failure<VatRecapResponse>(TaxReportErrors.InvalidPeriod);
        }

        (DateOnly start, DateOnly end) = TaxSql.Period(query.Year, query.Month);
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            SELECT i.number AS InvoiceNumber, i.invoice_date AS InvoiceDate, b.code AS BranchCode, c.code AS CustomerCode,
                   c.name AS CustomerName, c.tax_identity_npwp AS Npwp, c.tax_identity_nitku AS Nitku, c.tax_identity_is_pkp AS IsPkp,
                   (SELECT string_agg(DISTINCT t.code, ', ') FROM sales.sales_invoice_lines l
                    JOIN master.tax_codes t ON t.id = l.tax_code_id WHERE l.sales_invoice_id = i.id) AS TaxCodes,
                   i.subtotal AS Dpp,
                   (SELECT COALESCE(SUM(l.vat_tax_base), 0) FROM sales.sales_invoice_lines l WHERE l.sales_invoice_id = i.id) AS DppNilaiLain,
                   i.vat_amount AS Ppn
            FROM sales.sales_invoices i
            JOIN master.branches b ON b.id = i.branch_id
            JOIN master.customers c ON c.id = i.customer_id
            WHERE i.status IN ('Posted', 'PartiallyPaid', 'Paid') AND i.invoice_date BETWEEN @Start AND @End AND {TaxSql.Branch("i")}
            ORDER BY i.invoice_date, i.number;

            SELECT n.number AS CreditNoteNumber, n.date AS Date, i.number AS InvoiceNumber, c.name AS CustomerName,
                   c.tax_identity_npwp AS Npwp, n.subtotal AS Dpp, n.vat_amount AS Ppn
            FROM sales.sales_credit_notes n
            JOIN sales.sales_invoices i ON i.id = n.sales_invoice_id
            JOIN master.customers c ON c.id = n.customer_id
            WHERE n.date BETWEEN @Start AND @End AND n.vat_amount <> 0 AND {TaxSql.Branch("n")}
            ORDER BY n.date, n.number;

            SELECT v.number AS InvoiceNumber, v.vendor_invoice_number AS VendorInvoiceNumber, v.tax_invoice_number AS TaxInvoiceNumber,
                   v.invoice_date AS InvoiceDate, b.code AS BranchCode, vd.code AS VendorCode, vd.name AS VendorName,
                   vd.tax_identity_npwp AS Npwp, vd.tax_identity_nitku AS Nitku, v.subtotal AS Dpp,
                   (SELECT COALESCE(SUM(l.vat_tax_base), 0) FROM finance.vendor_invoice_lines l WHERE l.vendor_invoice_id = v.id) AS DppNilaiLain,
                   v.vat_amount AS Ppn
            FROM finance.vendor_invoices v
            JOIN master.branches b ON b.id = v.branch_id
            JOIN master.vendors vd ON vd.id = v.vendor_id
            WHERE v.status IN ('Posted', 'PartiallyPaid', 'Paid') AND v.vat_amount <> 0
              AND v.invoice_date BETWEEN @Start AND @End AND {TaxSql.Branch("v")}
            ORDER BY v.invoice_date, v.number;
            """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { Start = start, End = end, query.BranchId, scope.AllBranches, scope.BranchIds },
            cancellationToken: cancellationToken));

        List<OutputVatLine> output = [.. await multi.ReadAsync<OutputVatLine>()];
        List<OutputVatReturnLine> returns = [.. await multi.ReadAsync<OutputVatReturnLine>()];
        List<InputVatLine> input = [.. await multi.ReadAsync<InputVatLine>()];

        decimal outputVat = output.Sum(l => l.Ppn);
        decimal returnVat = returns.Sum(l => l.Ppn);
        decimal inputVat = input.Sum(l => l.Ppn);

        return new VatRecapResponse(
            query.Year, query.Month, output, returns, input, outputVat, returnVat, inputVat, outputVat - returnVat - inputVat);
    }
}

internal sealed class GetWithholdingRecapQueryHandler(IDbConnectionFactory dbConnectionFactory, IBranchAccess branchAccess)
    : IQueryHandler<GetWithholdingRecapQuery, WithholdingRecapResponse>
{
    public async Task<Result<WithholdingRecapResponse>> Handle(GetWithholdingRecapQuery query, CancellationToken cancellationToken)
    {
        if (query.Month is < 1 or > 12 || query.Year is < 2000 or > 2100)
        {
            return Result.Failure<WithholdingRecapResponse>(TaxReportErrors.InvalidPeriod);
        }

        (DateOnly start, DateOnly end) = TaxSql.Period(query.Year, query.Month);
        BranchScope scope = await branchAccess.GetScopeAsync(cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql =
            $"""
            SELECT * FROM (
                SELECT 'VendorInvoice' AS Source, v.number AS DocumentNumber, v.invoice_date AS Date, b.code AS BranchCode,
                       vd.code AS PayeeCode, vd.name AS PayeeName, vd.tax_identity_npwp AS Npwp, NULL::text AS Nik,
                       t.code AS TaxCode, t.income_tax_article AS Article, v.subtotal AS TaxBase,
                       v.income_tax_rate_percent AS RatePercent, v.income_tax_amount AS Amount
                FROM finance.vendor_invoices v
                JOIN master.branches b ON b.id = v.branch_id
                JOIN master.vendors vd ON vd.id = v.vendor_id
                JOIN master.tax_codes t ON t.id = v.income_tax_code_id
                WHERE v.status IN ('Posted', 'PartiallyPaid', 'Paid') AND v.income_tax_amount <> 0
                  AND v.invoice_date BETWEEN @Start AND @End AND {TaxSql.Branch("v")}
                UNION ALL
                SELECT 'PlasmaSettlement', s.number, s.settlement_date, b.code, f.code, f.name, f.tax_identity_npwp, f.nik,
                       t.code, t.income_tax_article, s.gross_income, s.income_tax_rate_percent, s.income_tax_amount
                FROM costing.plasma_settlements s
                JOIN master.branches b ON b.id = s.branch_id
                JOIN master.farmers f ON f.id = s.farmer_id
                JOIN master.tax_codes t ON t.id = s.income_tax_code_id
                WHERE s.status IN ('Approved', 'PartiallyPaid', 'Paid') AND s.income_tax_amount <> 0
                  AND s.settlement_date BETWEEN @Start AND @End AND {TaxSql.Branch("s")}) w
            ORDER BY w.TaxCode, w.Date, w.DocumentNumber;
            """;

        List<WithholdingLine> lines =
        [
            .. await connection.QueryAsync<WithholdingLine>(new CommandDefinition(
                sql,
                new { Start = start, End = end, query.BranchId, scope.AllBranches, scope.BranchIds },
                cancellationToken: cancellationToken))
        ];

        List<WithholdingTotal> totals =
        [
            .. lines
                .GroupBy(l => (l.TaxCode, l.Article))
                .Select(g => new WithholdingTotal(g.Key.TaxCode, g.Key.Article, g.Sum(l => l.TaxBase), g.Sum(l => l.Amount)))
        ];

        return new WithholdingRecapResponse(query.Year, query.Month, lines, totals, lines.Sum(l => l.Amount));
    }
}

internal static class TaxReportErrors
{
    public static readonly Error InvalidPeriod = Error.Problem(
        "TaxReports.InvalidPeriod",
        "The tax period needs a year between 2000 and 2100 and a month between 1 and 12");
}
