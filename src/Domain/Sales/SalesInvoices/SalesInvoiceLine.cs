using Domain.MasterData.TaxCodes;
using Domain.Sales.DeliveryOrders;
using SharedKernel;

namespace Domain.Sales.SalesInvoices;

public sealed class SalesInvoiceLine
{
    internal SalesInvoiceLine(Guid salesInvoiceId, int lineNumber, DeliveryOrderLine deliveryLine, TaxCalculation vat)
    {
        SalesInvoiceId = salesInvoiceId;
        LineNumber = lineNumber;
        DeliveryOrderId = deliveryLine.DeliveryOrderId;
        DeliveryOrderLineNumber = deliveryLine.LineNumber;
        ItemId = deliveryLine.ItemId;
        CycleId = deliveryLine.CycleId;
        Birds = deliveryLine.Birds;
        WeightKg = deliveryLine.WeightKg;

        // Own copies: a value object instance is never shared between two entities.
        PricePerKg = deliveryLine.PricePerKg with { };
        Amount = deliveryLine.Amount with { };
        TaxCodeId = vat.TaxCodeId ?? deliveryLine.TaxCodeId;
        VatRatePercent = vat.RatePercent;
        VatTaxBase = vat.TaxBase with { };
        VatAmount = vat.TaxAmount with { };
    }

    private SalesInvoiceLine()
    {
    }

    public Guid SalesInvoiceId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid DeliveryOrderId { get; private set; }
    public int DeliveryOrderLineNumber { get; private set; }
    public Guid ItemId { get; private set; }

    /// <summary>
    /// The cycle the birds come from (for profitability per cycle).
    /// </summary>
    public Guid CycleId { get; private set; }

    public int Birds { get; private set; }
    public decimal WeightKg { get; private set; }
    public Money PricePerKg { get; private set; }

    /// <summary>
    /// Excluding VAT.
    /// </summary>
    public Money Amount { get; private set; }

    public Guid? TaxCodeId { get; private set; }

    /// <summary>
    /// Rate in effect on the invoice date; zero when no VAT is charged (no code, exempt or not collected).
    /// </summary>
    public decimal VatRatePercent { get; private set; }

    /// <summary>
    /// DPP (tax base) the VAT is calculated on.
    /// </summary>
    public Money VatTaxBase { get; private set; }

    public Money VatAmount { get; private set; }

    /// <summary>
    /// Amount (excluding VAT) reduced by credit notes.
    /// </summary>
    public Money CreditedAmount { get; private set; } = new(0m);

    public Money CreditableAmount => Amount - CreditedAmount;

    /// <summary>
    /// Estimated cost of the birds sold (HPP), set when the invoice is posted: the cycle's running cost per kg ×
    /// weighed kg. The difference with the final cycle cost is journaled when the cycle closes.
    /// </summary>
    public Money CostAmount { get; private set; } = new(0m);

    internal void AddCredit(Money amount)
    {
        CreditedAmount += amount;
    }

    internal void SetCost(decimal costPerKg)
    {
        CostAmount = new Money(costPerKg * WeightKg);
    }
}
