using SharedKernel;

namespace Domain.Partnership.Contracts;

public sealed class ContractLiveBirdPrice
{
    internal ContractLiveBirdPrice(Guid contractId, decimal minWeightKg, decimal maxWeightKg, Money pricePerKg)
    {
        ContractId = contractId;
        MinWeightKg = minWeightKg;
        MaxWeightKg = maxWeightKg;
        PricePerKg = pricePerKg;
    }

    private ContractLiveBirdPrice()
    {
    }

    public Guid ContractId { get; private set; }

    /// <summary>
    /// Inclusive lower bound of the average body weight (kg/ekor).
    /// </summary>
    public decimal MinWeightKg { get; private set; }

    /// <summary>
    /// Exclusive upper bound of the average body weight (kg/ekor).
    /// </summary>
    public decimal MaxWeightKg { get; private set; }

    public Money PricePerKg { get; private set; }
}
