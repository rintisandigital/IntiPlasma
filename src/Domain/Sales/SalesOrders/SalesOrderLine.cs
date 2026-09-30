using SharedKernel;

namespace Domain.Sales.SalesOrders;

public sealed class SalesOrderLine
{
    internal SalesOrderLine(
        Guid salesOrderId,
        int lineNumber,
        Guid itemId,
        int birds,
        decimal estimatedWeightKg,
        Money pricePerKg,
        Guid? taxCodeId)
    {
        SalesOrderId = salesOrderId;
        LineNumber = lineNumber;
        ItemId = itemId;
        Birds = birds;
        EstimatedWeightKg = estimatedWeightKg;
        PricePerKg = pricePerKg;
        TaxCodeId = taxCodeId;
    }

    private SalesOrderLine()
    {
    }

    public Guid SalesOrderId { get; private set; }
    public int LineNumber { get; private set; }

    /// <summary>
    /// A live bird item.
    /// </summary>
    public Guid ItemId { get; private set; }

    /// <summary>
    /// Ordered birds (ekor); the firm quantity of the order.
    /// </summary>
    public int Birds { get; private set; }

    /// <summary>
    /// Expected total live weight; used for the order value and the credit check. The invoice uses the weighed kg.
    /// </summary>
    public decimal EstimatedWeightKg { get; private set; }

    /// <summary>
    /// Price per kg live weight, excluding VAT.
    /// </summary>
    public Money PricePerKg { get; private set; }

    public Guid? TaxCodeId { get; private set; }

    public int DeliveredBirds { get; private set; }
    public decimal DeliveredWeightKg { get; private set; }

    public int OutstandingBirds => Birds - DeliveredBirds;

    public Money EstimatedAmount => PricePerKg * EstimatedWeightKg;

    public Money OutstandingEstimatedAmount => EstimatedAmount * ((decimal)OutstandingBirds / Birds);

    internal void AddDelivered(int birds, decimal weightKg)
    {
        DeliveredBirds += birds;
        DeliveredWeightKg += weightKg;
    }
}
