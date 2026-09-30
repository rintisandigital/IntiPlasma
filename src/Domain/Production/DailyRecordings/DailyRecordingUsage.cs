using SharedKernel;

namespace Domain.Production.DailyRecordings;

/// <summary>
/// Feed or OVK used on the day, taken from the coop warehouse.
/// </summary>
public sealed class DailyRecordingUsage
{
    internal DailyRecordingUsage(Guid dailyRecordingId, Guid itemId, Guid uomId, decimal quantity, decimal baseQuantity)
    {
        DailyRecordingId = dailyRecordingId;
        ItemId = itemId;
        UomId = uomId;
        Quantity = quantity;
        BaseQuantity = baseQuantity;
        Value = Money.Zero;
    }

    private DailyRecordingUsage()
    {
    }

    public Guid DailyRecordingId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid UomId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal BaseQuantity { get; private set; }

    /// <summary>
    /// Value at which the quantity left the coop warehouse; used to put it back exactly when the recording is revised.
    /// </summary>
    public Money Value { get; private set; }

    internal void SetValue(Money value)
    {
        Value = value;
    }
}
