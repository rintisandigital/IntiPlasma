using System.Data.Common;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Dapper;
using Domain.Finance.FiscalPeriods;
using SharedKernel;

namespace Application.Finance.FiscalPeriods;

/// <summary>
/// What stands in the way of closing a period. Blocking checks prevent closing (unposted journals, automatic journals
/// not yet posted or failed); warnings list documents of the period that are not finished and can no longer be posted
/// once the period is closed.
/// </summary>
public sealed record GetPeriodClosingChecklistQuery(Guid FiscalPeriodId) : IQuery<PeriodClosingChecklist>;

public sealed record PeriodClosingChecklist(
    Guid FiscalPeriodId,
    int Year,
    int Month,
    string Status,
    bool CanClose,
    IReadOnlyList<ClosingCheck> Checks);

/// <param name="Count">Number of offending documents/messages; zero means the check passes.</param>
public sealed record ClosingCheck(string Code, string Description, bool Blocking, int Count);

internal static class PeriodClosingChecks
{
    private static readonly (string Code, string Description, bool Blocking, string Sql)[] Checks =
    [
        ("UnpostedJournals", "Jurnal manual draft/approved belum diposting", true,
            "SELECT COUNT(*) FROM finance.journal_entries WHERE status IN ('Draft', 'Approved') AND date BETWEEN @Start AND @End"),
        ("PendingAutoJournals", "Event jurnal otomatis belum diproses (tunggu sebentar lalu cek lagi)", true,
            "SELECT COUNT(*) FROM infrastructure.outbox_messages WHERE processed_on_utc IS NULL"),
        ("FailedAutoJournals", "Jurnal otomatis gagal (dead letter) — perbaiki penyebabnya lalu coba ulang", true,
            "SELECT COUNT(*) FROM infrastructure.outbox_messages WHERE error IS NOT NULL"),
        ("DraftSalesInvoices", "Invoice penjualan masih draft", false,
            "SELECT COUNT(*) FROM sales.sales_invoices WHERE status = 'Draft' AND invoice_date BETWEEN @Start AND @End"),
        ("UninvoicedDeliveries", "DO terkirim belum ditagih", false,
            "SELECT COUNT(*) FROM sales.delivery_orders WHERE status = 'Delivered' AND delivery_date BETWEEN @Start AND @End"),
        ("DraftVendorInvoices", "Invoice vendor masih draft", false,
            "SELECT COUNT(*) FROM finance.vendor_invoices WHERE status = 'Draft' AND invoice_date BETWEEN @Start AND @End"),
        ("OpenPaymentVouchers", "Payment voucher draft/approved belum dibayar", false,
            "SELECT COUNT(*) FROM finance.payment_vouchers WHERE status IN ('Draft', 'Approved') AND payment_date BETWEEN @Start AND @End"),
        ("OpenCashTransactions", "Kas masuk/keluar draft/approved belum diposting", false,
            "SELECT COUNT(*) FROM finance.cash_transactions WHERE status IN ('Draft', 'Approved') AND date BETWEEN @Start AND @End"),
        ("DraftSettlements", "Settlement plasma masih draft", false,
            "SELECT COUNT(*) FROM costing.plasma_settlements WHERE status = 'Draft' AND settlement_date BETWEEN @Start AND @End"),
        ("UnreconciledBanks", "Rekening bank belum direkonsiliasi sampai akhir periode", false,
            """
            SELECT COUNT(*) FROM finance.cash_bank_accounts cb
            WHERE cb.type = 'Bank' AND cb.is_active
              AND NOT EXISTS (SELECT 1 FROM finance.bank_reconciliations r
                              WHERE r.cash_bank_account_id = cb.id AND r.status = 'Completed' AND r.statement_date >= @End)
            """)
    ];

    public static async Task<IReadOnlyList<ClosingCheck>> RunAsync(
        IDbConnectionFactory dbConnectionFactory,
        FiscalPeriod period,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        string sql = string.Join(";\n", Checks.Select(c => c.Sql));

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(new CommandDefinition(
            sql, new { Start = period.StartDate, End = period.EndDate }, cancellationToken: cancellationToken));

        var results = new List<ClosingCheck>();
        foreach ((string code, string description, bool blocking, string _) in Checks)
        {
            long count = await multi.ReadSingleAsync<long>();
            results.Add(new ClosingCheck(code, description, blocking, (int)count));
        }

        return results;
    }
}

internal sealed class GetPeriodClosingChecklistQueryHandler(IApplicationDbContext context, IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetPeriodClosingChecklistQuery, PeriodClosingChecklist>
{
    public async Task<Result<PeriodClosingChecklist>> Handle(GetPeriodClosingChecklistQuery query, CancellationToken cancellationToken)
    {
        FiscalPeriod? period = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(
            context.FiscalPeriods, p => p.Id == query.FiscalPeriodId, cancellationToken);

        if (period is null)
        {
            return Result.Failure<PeriodClosingChecklist>(FiscalPeriodErrors.NotFound(query.FiscalPeriodId));
        }

        IReadOnlyList<ClosingCheck> checks = await PeriodClosingChecks.RunAsync(dbConnectionFactory, period, cancellationToken);

        return new PeriodClosingChecklist(
            period.Id, period.Year, period.Month, period.Status.ToString(),
            period.IsOpen && !checks.Any(c => c.Blocking && c.Count > 0), checks);
    }
}
