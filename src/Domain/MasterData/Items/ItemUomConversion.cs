namespace Domain.MasterData.Items;

public sealed class ItemUomConversion
{
    internal ItemUomConversion(Guid itemId, Guid uomId, decimal factor)
    {
        ItemId = itemId;
        UomId = uomId;
        Factor = factor;
    }

    public Guid ItemId { get; private set; }
    public Guid UomId { get; private set; }

    /// <summary>
    /// Number of base units in one <see cref="UomId"/>, e.g. 50 (KG) for one SAK of feed.
    /// </summary>
    public decimal Factor { get; private set; }
}
