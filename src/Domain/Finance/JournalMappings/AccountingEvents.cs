namespace Domain.Finance.JournalMappings;

/// <summary>
/// Catalog of accounting events the operational modules publish to the auto journal engine, with the amount
/// components each event carries. Finance maps every component to a debit and a credit account
/// (<see cref="JournalMapping"/>), so no account is hardcoded in the operational modules.
/// </summary>
public static class AccountingEvents
{
    public const string PurchaseReceipt = "PurchaseReceipt";
    public const string VendorInvoice = "VendorInvoice";
    public const string VendorPayment = "VendorPayment";
    public const string StockTransferToCycle = "StockTransferToCycle";
    public const string SalesInvoice = "SalesInvoice";
    public const string CustomerReceipt = "CustomerReceipt";
    public const string PlasmaSettlement = "PlasmaSettlement";
    public const string PlasmaPayment = "PlasmaPayment";
    public const string StockReturnFromCycle = "StockReturnFromCycle";
    public const string CustomerAdvanceApplied = "CustomerAdvanceApplied";
    public const string SalesCreditNote = "SalesCreditNote";
    public const string CycleCostAdjustment = "CycleCostAdjustment";

    /// <summary>
    /// Journal source of cash-in/cash-out transactions; their lines name the accounts, so they have no mapping.
    /// </summary>
    public const string CashTransaction = "CashTransaction";

    /// <summary>
    /// Journal source of transfers between cash/bank accounts (Dr destination / Cr source); no mapping.
    /// </summary>
    public const string BankTransfer = "BankTransfer";

    public static readonly IReadOnlyList<AccountingEventDefinition> All =
    [
        new(PurchaseReceipt, "Penerimaan sapronak (DOC, pakan, OVK) ke gudang",
        [
            new("DocReceived", "Nilai DOC diterima"),
            new("FeedReceived", "Nilai pakan diterima"),
            new("OvkReceived", "Nilai OVK diterima")
        ]),
        new(VendorInvoice, "Tagihan vendor (invoice pembelian)",
        [
            new("GoodsValue", "Nilai penerimaan barang yang ditagih (hapus hutang belum ditagih)"),
            new("PriceVariance", "Selisih harga invoice terhadap harga PO (negatif = lebih murah)"),
            new("InputVat", "PPN masukan"),
            new("IncomeTaxWithheld", "PPh dipotong")
        ]),
        new(VendorPayment, "Pembayaran ke vendor",
        [
            new("Paid", "Jumlah dibayar (akun kas/bank bisa ditentukan per transaksi)")
        ]),
        new(StockTransferToCycle, "Transfer sapronak ke kandang (siklus berjalan)",
        [
            new("DocIssued", "Nilai DOC ditransfer"),
            new("FeedIssued", "Nilai pakan ditransfer"),
            new("OvkIssued", "Nilai OVK ditransfer")
        ]),
        new(SalesInvoice, "Invoice penjualan ayam",
        [
            new("LiveBirdSales", "Penjualan ayam hidup (DPP)"),
            new("OutputVat", "PPN keluaran"),
            new("CostOfGoodsSold", "HPP ayam terjual (estimasi biaya per kg siklus saat posting)")
        ]),
        new(CustomerReceipt, "Penerimaan pembayaran customer",
        [
            new("Received", "Jumlah diterima untuk invoice (akun kas/bank bisa ditentukan per transaksi)"),
            new("Advance", "Uang muka penjualan (sisa penerimaan yang belum dialokasikan ke invoice)")
        ]),
        new(CustomerAdvanceApplied, "Penerapan uang muka penjualan ke invoice",
        [
            new("Applied", "Uang muka yang diterapkan ke invoice")
        ]),
        new(SalesCreditNote, "Nota kredit / retur penjualan",
        [
            new("SalesReturn", "Potongan/retur penjualan (DPP)"),
            new("OutputVat", "Koreksi PPN keluaran")
        ]),
        new(StockReturnFromCycle, "Retur sisa sapronak dari kandang ke gudang induk",
        [
            new("FeedReturned", "Nilai pakan diretur"),
            new("OvkReturned", "Nilai OVK diretur")
        ]),
        new(PlasmaSettlement, "Settlement plasma (rugi/laba kemitraan)",
        [
            new("PlasmaIncome", "Pendapatan plasma (hak plasma)"),
            new("IncomeTaxWithheld", "PPh dipotong dari pendapatan plasma"),
            new("Deduction", "Potongan hutang/denda plasma"),
            new("PlasmaDeficit", "Rugi plasma (hasil negatif) yang menjadi piutang plasma")
        ]),
        new(CycleCostAdjustment, "Penyesuaian HPP saat tutup siklus (biaya final − HPP estimasi di invoice)",
        [
            new("CostOfGoodsSold", "Selisih HPP (negatif = HPP estimasi terlalu tinggi)")
        ]),
        new(PlasmaPayment, "Pembayaran ke plasma",
        [
            new("Paid", "Jumlah dibayar (akun kas/bank bisa ditentukan per transaksi)")
        ])
    ];

    public static AccountingEventDefinition? Find(string eventType) =>
        All.FirstOrDefault(e => e.Code == eventType);
}

public sealed record AccountingEventDefinition(
    string Code,
    string Description,
    IReadOnlyList<AccountingComponentDefinition> Components)
{
    public bool HasComponent(string component) => Components.Any(c => c.Code == component);
}

public sealed record AccountingComponentDefinition(string Code, string Description);
