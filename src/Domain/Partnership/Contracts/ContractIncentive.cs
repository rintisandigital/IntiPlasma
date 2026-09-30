using SharedKernel;

namespace Domain.Partnership.Contracts;

/// <summary>
/// A bonus/insentif or potongan/denda rule, e.g. "Bonus FCR: FCR 1.40–1.50 → Rp 200/kg".
/// The rule applies when the metric of the closed cycle falls within [RangeFrom, RangeTo].
/// </summary>
public sealed class ContractIncentive
{
    internal ContractIncentive(
        Guid contractId,
        int lineNumber,
        string name,
        IncentiveKind kind,
        IncentiveMetric metric,
        decimal? rangeFrom,
        decimal? rangeTo,
        Money amount,
        IncentiveBasis basis)
    {
        ContractId = contractId;
        LineNumber = lineNumber;
        Name = name;
        Kind = kind;
        Metric = metric;
        RangeFrom = rangeFrom;
        RangeTo = rangeTo;
        Amount = amount;
        Basis = basis;
    }

    private ContractIncentive()
    {
    }

    public Guid ContractId { get; private set; }
    public int LineNumber { get; private set; }
    public string Name { get; private set; }
    public IncentiveKind Kind { get; private set; }
    public IncentiveMetric Metric { get; private set; }
    public decimal? RangeFrom { get; private set; }
    public decimal? RangeTo { get; private set; }
    public Money Amount { get; private set; }
    public IncentiveBasis Basis { get; private set; }
}
