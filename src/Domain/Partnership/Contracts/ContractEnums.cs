namespace Domain.Partnership.Contracts;

public enum ContractScheme
{
    /// <summary>
    /// Sistem harga kontrak.
    /// </summary>
    PriceContract = 1,

    /// <summary>
    /// Sistem bagi hasil.
    /// </summary>
    ProfitSharing = 2
}

public enum ContractStatus
{
    Draft = 1,
    Active = 2,
    Inactive = 3
}

public enum IncentiveKind
{
    /// <summary>
    /// Bonus / insentif (added to the plasma income).
    /// </summary>
    Bonus = 1,

    /// <summary>
    /// Potongan / denda (deducted from the plasma income).
    /// </summary>
    Deduction = 2
}

public enum IncentiveMetric
{
    /// <summary>
    /// Always applies (e.g. a fixed allowance).
    /// </summary>
    None = 0,

    /// <summary>
    /// Feed Conversion Ratio.
    /// </summary>
    Fcr = 1,

    /// <summary>
    /// Indeks Performa.
    /// </summary>
    Ip = 2,

    /// <summary>
    /// Deplesi (mortality + culling) in percent.
    /// </summary>
    Depletion = 3,

    /// <summary>
    /// Average harvest body weight in kg.
    /// </summary>
    AverageWeight = 4
}

public enum IncentiveBasis
{
    /// <summary>
    /// Amount per kg of harvested live weight.
    /// </summary>
    PerKg = 1,

    /// <summary>
    /// Amount per harvested bird.
    /// </summary>
    PerBird = 2,

    /// <summary>
    /// Lump sum per cycle.
    /// </summary>
    Fixed = 3
}
