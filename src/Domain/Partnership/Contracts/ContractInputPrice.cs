using SharedKernel;

namespace Domain.Partnership.Contracts;

public sealed class ContractInputPrice
{
    internal ContractInputPrice(Guid contractId, Guid itemId, Money price)
    {
        ContractId = contractId;
        ItemId = itemId;
        Price = price;
    }

    private ContractInputPrice()
    {
    }

    public Guid ContractId { get; private set; }
    public Guid ItemId { get; private set; }

    /// <summary>
    /// Price per base unit of the item.
    /// </summary>
    public Money Price { get; private set; }
}
