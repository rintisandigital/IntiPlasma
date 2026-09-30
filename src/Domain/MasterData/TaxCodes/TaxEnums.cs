namespace Domain.MasterData.TaxCodes;

public enum TaxType
{
    Vat = 1,
    IncomeTax = 2
}

public enum VatTreatment
{
    /// <summary>
    /// PPN terutang dan dipungut.
    /// </summary>
    Taxable = 1,

    /// <summary>
    /// PPN dibebaskan (e.g. certain livestock, DOC, animal feed facilities).
    /// </summary>
    Exempt = 2,

    /// <summary>
    /// PPN tidak dipungut.
    /// </summary>
    NotCollected = 3
}

public enum IncomeTaxArticle
{
    Pph21 = 21,
    Pph22 = 22,
    Pph23 = 23,
    Pph4Ayat2 = 42
}
