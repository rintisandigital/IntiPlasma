using Domain.MasterData.TaxCodes;
using SharedKernel;

namespace Domain.Finance.Payables;

public sealed class VendorInvoiceLine
{
    internal VendorInvoiceLine(
        Guid vendorInvoiceId,
        int lineNumber,
        VendorInvoiceLineInput input,
        Money amount,
        Money goodsValue,
        TaxCalculation vat)
    {
        VendorInvoiceId = vendorInvoiceId;
        LineNumber = lineNumber;
        GoodsReceiptId = input.Receipt.Id;
        GoodsReceiptLineNumber = input.ReceiptLineNumber;
        PurchaseOrderId = input.OrderLine.PurchaseOrderId;
        PurchaseOrderLineNumber = input.OrderLine.LineNumber;
        ItemId = input.OrderLine.ItemId;
        UomId = input.OrderLine.UomId;
        Quantity = input.Quantity;

        // Own copies: a value object instance is never shared between two entities.
        OrderUnitPrice = input.OrderLine.UnitPrice with { };
        UnitPrice = input.UnitPrice with { };
        Amount = amount;
        GoodsValue = goodsValue with { };
        TaxCodeId = input.OrderLine.TaxCodeId;
        VatRatePercent = vat.RatePercent;
        VatTaxBase = vat.TaxBase with { };
        VatAmount = vat.TaxAmount with { };
        PriceDeviationPercent = decimal.Round(
            Math.Abs(UnitPrice.Amount - OrderUnitPrice.Amount) / OrderUnitPrice.Amount * 100m, 4);
    }

    private VendorInvoiceLine()
    {
    }

    public Guid VendorInvoiceId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid GoodsReceiptId { get; private set; }
    public int GoodsReceiptLineNumber { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public int PurchaseOrderLineNumber { get; private set; }
    public Guid ItemId { get; private set; }

    /// <summary>
    /// The order line's unit.
    /// </summary>
    public Guid UomId { get; private set; }

    public decimal Quantity { get; private set; }

    /// <summary>
    /// Price per order unit on the purchase order, excluding VAT.
    /// </summary>
    public Money OrderUnitPrice { get; private set; }

    /// <summary>
    /// Price per order unit on the vendor invoice, excluding VAT.
    /// </summary>
    public Money UnitPrice { get; private set; }

    public Money Amount { get; private set; }

    /// <summary>
    /// Receipt value of the billed quantity (cleared from GRNI).
    /// </summary>
    public Money GoodsValue { get; private set; }

    public Guid? TaxCodeId { get; private set; }
    public decimal VatRatePercent { get; private set; }
    public Money VatTaxBase { get; private set; }
    public Money VatAmount { get; private set; }

    /// <summary>
    /// |invoice price − order price| / order price, in percent.
    /// </summary>
    public decimal PriceDeviationPercent { get; private set; }
}
