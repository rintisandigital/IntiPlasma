using Domain.Common;
using SharedKernel;

namespace Domain.MasterData.TaxCodes;

/// <summary>
/// Tax code for PPN (VAT) or PPh (income tax withholding). Rates are effective-dated, so a rate change
/// (e.g. PPN 11% → 12%) never alters historical documents. Nothing is hardcoded: every tariff is data.
/// </summary>
public sealed class TaxCode : AggregateRoot
{
    private readonly List<TaxRate> _rates = [];

    private TaxCode(
        Guid id,
        string code,
        string name,
        TaxType type,
        VatTreatment? vatTreatment,
        IncomeTaxArticle? incomeTaxArticle)
        : base(id)
    {
        Code = code;
        Name = name;
        Type = type;
        VatTreatment = vatTreatment;
        IncomeTaxArticle = incomeTaxArticle;
        IsActive = true;
    }

    private TaxCode()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public TaxType Type { get; private set; }

    /// <summary>
    /// Only for <see cref="TaxType.Vat"/>.
    /// </summary>
    public VatTreatment? VatTreatment { get; private set; }

    /// <summary>
    /// Only for <see cref="TaxType.IncomeTax"/>.
    /// </summary>
    public IncomeTaxArticle? IncomeTaxArticle { get; private set; }

    public bool IsActive { get; private set; }
    public IReadOnlyCollection<TaxRate> Rates => [.. _rates];

    public static TaxCode CreateVat(string code, string name, VatTreatment treatment) =>
        new(Guid.CreateVersion7(), Codes.Normalize(code), name.Trim(), TaxType.Vat, treatment, null);

    public static TaxCode CreateIncomeTax(string code, string name, IncomeTaxArticle article) =>
        new(Guid.CreateVersion7(), Codes.Normalize(code), name.Trim(), TaxType.IncomeTax, null, article);

    public void Update(string name, bool isActive)
    {
        Name = name.Trim();
        IsActive = isActive;
    }

    /// <summary>
    /// Replaces the rate history. Each rate applies from its effective date until the next one.
    /// </summary>
    public Result SetRates(IEnumerable<(DateOnly EffectiveFrom, decimal RatePercent, decimal TaxBaseRatio)> rates)
    {
        var list = rates.ToList();

        if (list.Count == 0)
        {
            return Result.Failure(TaxCodeErrors.RateRequired);
        }

        if (list.GroupBy(r => r.EffectiveFrom).Any(g => g.Count() > 1))
        {
            return Result.Failure(TaxCodeErrors.DuplicateEffectiveDate);
        }

        if (list.Exists(r => r.RatePercent is < 0 or > 100))
        {
            return Result.Failure(TaxCodeErrors.InvalidRate);
        }

        if (list.Exists(r => r.TaxBaseRatio is <= 0 or > 1))
        {
            return Result.Failure(TaxCodeErrors.InvalidTaxBaseRatio);
        }

        _rates.Clear();
        _rates.AddRange(list.Select(r => new TaxRate(Id, r.EffectiveFrom, r.RatePercent, r.TaxBaseRatio)));

        return Result.Success();
    }

    public TaxRate? GetRateOn(DateOnly date) =>
        _rates
            .Where(r => r.EffectiveFrom <= date)
            .MaxBy(r => r.EffectiveFrom);

    /// <summary>
    /// VAT on an amount (excluding VAT) at the rate in effect on the date. Exempt and not-collected VAT codes yield
    /// no VAT. Also used for income tax withholding (PPh), where the amount is the withholding base.
    /// </summary>
    public Result<TaxCalculation> Calculate(Money amount, DateOnly date)
    {
        if (Type == TaxType.Vat && VatTreatment != TaxCodes.VatTreatment.Taxable)
        {
            return TaxCalculation.None with { TaxCodeId = Id };
        }

        TaxRate? rate = GetRateOn(date);
        if (rate is null)
        {
            return Result.Failure<TaxCalculation>(TaxCodeErrors.NoRate(Code, date));
        }

        Money taxBase = amount * rate.TaxBaseRatio;

        return new TaxCalculation(Id, rate.RatePercent, taxBase, taxBase * (rate.RatePercent / 100m));
    }
}

/// <param name="TaxBase">DPP: the amount × the rate's tax base ratio.</param>
public sealed record TaxCalculation(Guid? TaxCodeId, decimal RatePercent, Money TaxBase, Money TaxAmount)
{
    public static readonly TaxCalculation None = new(null, 0m, Money.Zero, Money.Zero);
}
