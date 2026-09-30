namespace Domain.MasterData.TaxCodes;

public sealed class TaxRate
{
    internal TaxRate(Guid taxCodeId, DateOnly effectiveFrom, decimal ratePercent, decimal taxBaseRatio)
    {
        TaxCodeId = taxCodeId;
        EffectiveFrom = effectiveFrom;
        RatePercent = ratePercent;
        TaxBaseRatio = taxBaseRatio;
    }

    public Guid TaxCodeId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }

    /// <summary>
    /// Tariff in percent, e.g. 12 for PPN 12% or 2 for PPh 23.
    /// </summary>
    public decimal RatePercent { get; private set; }

    /// <summary>
    /// Fraction of the price used as tax base (DPP). 1 for a normal base; 11/12 for "DPP nilai lain"
    /// (PPN 12% on non-luxury goods since 2025, which yields an effective 11%).
    /// </summary>
    public decimal TaxBaseRatio { get; private set; }
}
