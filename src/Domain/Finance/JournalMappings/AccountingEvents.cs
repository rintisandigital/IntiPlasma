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
            new("GoodsValue", "DPP barang yang sudah diterima"),
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
            new("CostOfGoodsSold", "HPP ayam terjual")
        ]),
        new(CustomerReceipt, "Penerimaan pembayaran customer",
        [
            new("Received", "Jumlah diterima (akun kas/bank bisa ditentukan per transaksi)")
        ]),
        new(PlasmaSettlement, "Settlement plasma (rugi/laba kemitraan)",
        [
            new("PlasmaIncome", "Pendapatan plasma (hak plasma)"),
            new("IncomeTaxWithheld", "PPh dipotong dari pendapatan plasma"),
            new("Deduction", "Potongan hutang/denda plasma")
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
