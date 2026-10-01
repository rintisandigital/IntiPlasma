namespace Domain.Documents.Attachments;

/// <summary>
/// Entities that carry attachments (<see cref="AttachmentLink.OwnerType"/>).
/// </summary>
public static class AttachmentOwnerTypes
{
    public const string Farmer = "Farmer";
    public const string Coop = "Coop";
    public const string Vendor = "Vendor";
    public const string Customer = "Customer";
    public const string Contract = "Contract";
    public const string Cycle = "Cycle";
    public const string Harvest = "Harvest";
    public const string DailyRecording = "DailyRecording";
    public const string DailyRecordingRevision = "DailyRecordingRevision";
    public const string GoodsReceipt = "GoodsReceipt";
    public const string StockReturn = "StockReturn";
    public const string StockTransfer = "StockTransfer";
    public const string PurchaseOrder = "PurchaseOrder";
    public const string SalesOrder = "SalesOrder";
    public const string DeliveryOrder = "DeliveryOrder";
    public const string VendorInvoice = "VendorInvoice";
    public const string PaymentVoucher = "PaymentVoucher";
    public const string CashTransaction = "CashTransaction";
    public const string CustomerReceipt = "CustomerReceipt";
    public const string Journal = "Journal";
    public const string PlasmaSettlement = "PlasmaSettlement";
}
