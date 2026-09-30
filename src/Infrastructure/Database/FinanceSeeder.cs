using Domain.Finance.Accounts;
using Domain.Finance.JournalMappings;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database;

/// <summary>
/// Seeds a starter chart of accounts for an inti-plasma broiler company and default auto journal mappings,
/// but only into an empty chart of accounts, so it never overwrites what finance has configured.
/// Everything seeded here can be renamed, deactivated or re-mapped afterwards.
/// </summary>
internal static class FinanceSeeder
{
    private const AccountType A = AccountType.Asset;
    private const AccountType L = AccountType.Liability;
    private const AccountType E = AccountType.Equity;
    private const AccountType R = AccountType.Revenue;
    private const AccountType X = AccountType.Expense;

    // (code, name, type, parent code, postable, normal balance override for contra accounts)
    private static readonly (string Code, string Name, AccountType Type, string? Parent, bool Postable, BalanceSide? Normal)[] ChartOfAccounts =
    [
        ("1", "Aset", A, null, false, null),
        ("1-1", "Aset Lancar", A, "1", false, null),
        ("1-1100", "Kas", A, "1-1", false, null),
        ("1-1101", "Kas Besar", A, "1-1100", true, null),
        ("1-1102", "Kas Kecil", A, "1-1100", true, null),
        ("1-1200", "Bank", A, "1-1", false, null),
        ("1-1201", "Bank Operasional", A, "1-1200", true, null),
        ("1-1300", "Piutang", A, "1-1", false, null),
        ("1-1301", "Piutang Usaha", A, "1-1300", true, null),
        ("1-1302", "Piutang Plasma", A, "1-1300", true, null),
        ("1-1303", "Piutang Karyawan", A, "1-1300", true, null),
        ("1-1400", "Persediaan Sapronak", A, "1-1", false, null),
        ("1-1401", "Persediaan DOC", A, "1-1400", true, null),
        ("1-1402", "Persediaan Pakan", A, "1-1400", true, null),
        ("1-1403", "Persediaan OVK", A, "1-1400", true, null),
        ("1-1500", "Persediaan Ayam Dalam Proses", A, "1-1", false, null),
        ("1-1501", "Ayam Dalam Proses (Siklus Berjalan)", A, "1-1500", true, null),
        ("1-1600", "Pajak Dibayar Dimuka", A, "1-1", false, null),
        ("1-1601", "PPN Masukan", A, "1-1600", true, null),
        ("1-1602", "PPh 22 Dibayar Dimuka", A, "1-1600", true, null),
        ("1-1603", "PPh 23 Dibayar Dimuka", A, "1-1600", true, null),
        ("1-1700", "Uang Muka", A, "1-1", false, null),
        ("1-1701", "Uang Muka Pembelian", A, "1-1700", true, null),
        ("1-2", "Aset Tetap", A, "1", false, null),
        ("1-2101", "Tanah", A, "1-2", true, null),
        ("1-2201", "Bangunan Kandang", A, "1-2", true, null),
        ("1-2301", "Peralatan Kandang", A, "1-2", true, null),
        ("1-2401", "Kendaraan", A, "1-2", true, null),
        ("1-2901", "Akumulasi Penyusutan Aset Tetap", A, "1-2", true, BalanceSide.Credit),

        ("2", "Liabilitas", L, null, false, null),
        ("2-1", "Liabilitas Jangka Pendek", L, "2", false, null),
        ("2-1101", "Hutang Usaha", L, "2-1", true, null),
        ("2-1102", "Hutang Belum Ditagih (GRNI)", L, "2-1", true, null),
        ("2-1201", "Hutang Plasma", L, "2-1", true, null),
        ("2-1300", "Hutang Pajak", L, "2-1", false, null),
        ("2-1301", "PPN Keluaran", L, "2-1300", true, null),
        ("2-1302", "Hutang PPh 21", L, "2-1300", true, null),
        ("2-1303", "Hutang PPh 23", L, "2-1300", true, null),
        ("2-1304", "Hutang PPh 4 Ayat 2", L, "2-1300", true, null),
        ("2-1401", "Biaya Yang Masih Harus Dibayar", L, "2-1", true, null),
        ("2-1501", "Uang Muka Penjualan", L, "2-1", true, null),
        ("2-2", "Liabilitas Jangka Panjang", L, "2", false, null),
        ("2-2101", "Hutang Bank", L, "2-2", true, null),

        ("3", "Ekuitas", E, null, false, null),
        ("3-1101", "Modal Disetor", E, "3", true, null),
        ("3-2101", "Laba Ditahan", E, "3", true, null),
        ("3-3101", "Laba Tahun Berjalan", E, "3", true, null),

        ("4", "Pendapatan Usaha", R, null, false, null),
        ("4-1101", "Penjualan Ayam Hidup", R, "4", true, null),
        ("4-1102", "Penjualan Lain-lain", R, "4", true, null),
        ("4-1901", "Potongan & Retur Penjualan", R, "4", true, BalanceSide.Debit),

        ("5", "Harga Pokok Penjualan", X, null, false, null),
        ("5-1101", "HPP Ayam Hidup", X, "5", true, null),
        ("5-1201", "Beban Kemitraan Plasma", X, "5", true, null),
        ("5-1301", "Selisih Persediaan", X, "5", true, null),

        ("6", "Beban Operasional", X, null, false, null),
        ("6-1101", "Beban Gaji & Tunjangan", X, "6", true, null),
        ("6-1201", "Beban Transportasi", X, "6", true, null),
        ("6-1301", "Beban Penyusutan", X, "6", true, null),
        ("6-1401", "Beban Listrik & Air", X, "6", true, null),
        ("6-1901", "Beban Umum Lain-lain", X, "6", true, null),

        ("7", "Pendapatan Lain-lain", R, null, false, null),
        ("7-1101", "Pendapatan Bunga", R, "7", true, null),
        ("7-1901", "Pendapatan Lain-lain", R, "7", true, null),

        ("8", "Beban Lain-lain", X, null, false, null),
        ("8-1101", "Beban Administrasi Bank", X, "8", true, null),
        ("8-1201", "Beban Bunga", X, "8", true, null)
    ];

    // (event, component, debit account code, credit account code)
    private static readonly (string Event, string Component, string Debit, string Credit)[] Mappings =
    [
        (AccountingEvents.PurchaseReceipt, "DocReceived", "1-1401", "2-1102"),
        (AccountingEvents.PurchaseReceipt, "FeedReceived", "1-1402", "2-1102"),
        (AccountingEvents.PurchaseReceipt, "OvkReceived", "1-1403", "2-1102"),
        (AccountingEvents.VendorInvoice, "GoodsValue", "2-1102", "2-1101"),
        (AccountingEvents.VendorInvoice, "InputVat", "1-1601", "2-1101"),
        (AccountingEvents.VendorInvoice, "IncomeTaxWithheld", "2-1101", "2-1303"),
        (AccountingEvents.VendorPayment, "Paid", "2-1101", "1-1201"),
        (AccountingEvents.StockTransferToCycle, "DocIssued", "1-1501", "1-1401"),
        (AccountingEvents.StockTransferToCycle, "FeedIssued", "1-1501", "1-1402"),
        (AccountingEvents.StockTransferToCycle, "OvkIssued", "1-1501", "1-1403"),
        (AccountingEvents.SalesInvoice, "LiveBirdSales", "1-1301", "4-1101"),
        (AccountingEvents.SalesInvoice, "OutputVat", "1-1301", "2-1301"),
        (AccountingEvents.SalesInvoice, "CostOfGoodsSold", "5-1101", "1-1501"),
        (AccountingEvents.CustomerReceipt, "Received", "1-1201", "1-1301"),
        (AccountingEvents.PlasmaSettlement, "PlasmaIncome", "5-1201", "2-1201"),
        (AccountingEvents.PlasmaSettlement, "IncomeTaxWithheld", "2-1201", "2-1303"),
        (AccountingEvents.PlasmaSettlement, "Deduction", "2-1201", "1-1302"),
        (AccountingEvents.PlasmaPayment, "Paid", "2-1201", "1-1201"),
        (AccountingEvents.StockReturnFromCycle, "FeedReturned", "1-1402", "1-1501"),
        (AccountingEvents.StockReturnFromCycle, "OvkReturned", "1-1403", "1-1501")
    ];

    public static async Task SeedAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        Dictionary<string, Guid> accountIds;

        if (await dbContext.Accounts.AnyAsync(cancellationToken))
        {
            accountIds = await dbContext.Accounts.ToDictionaryAsync(a => a.Code, a => a.Id, cancellationToken);
        }
        else
        {
            var accounts = new Dictionary<string, Account>(StringComparer.Ordinal);

            foreach ((string code, string name, AccountType type, string? parent, bool postable, BalanceSide? normal) in ChartOfAccounts)
            {
                Account account = Account.Create(
                    code, name, type, parent is null ? null : accounts[parent], postable, normal).Value;

                accounts.Add(code, account);
            }

            dbContext.Accounts.AddRange(accounts.Values);
            accountIds = accounts.ToDictionary(a => a.Key, a => a.Value.Id);
        }

        await SeedMissingMappingsAsync(dbContext, accountIds, cancellationToken);
    }

    /// <summary>
    /// Adds the default mapping of an accounting event that has no default mapping yet (e.g. an event introduced by a
    /// later release), provided every account it needs still exists under its seeded code. Existing mappings are never touched.
    /// </summary>
    private static async Task SeedMissingMappingsAsync(
        ApplicationDbContext dbContext,
        Dictionary<string, Guid> accountIds,
        CancellationToken cancellationToken)
    {
        List<string> mappedEvents = await dbContext.JournalMappings
            .Where(m => m.BranchId == null)
            .Select(m => m.EventType)
            .ToListAsync(cancellationToken);

        foreach (IGrouping<string, (string Event, string Component, string Debit, string Credit)> group in
                 Mappings.Where(m => !mappedEvents.Contains(m.Event)).GroupBy(m => m.Event))
        {
            if (!group.All(m => accountIds.ContainsKey(m.Debit) && accountIds.ContainsKey(m.Credit)))
            {
                continue;
            }

            JournalMapping mapping = JournalMapping.Create(
                group.Key,
                branchId: null,
                "Mapping default (seed)",
                [.. group.Select(m => (m.Component, accountIds[m.Debit], accountIds[m.Credit], (Guid?)null))]).Value;

            dbContext.JournalMappings.Add(mapping);
        }
    }
}
