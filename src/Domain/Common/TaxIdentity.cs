using SharedKernel;

namespace Domain.Common;

/// <summary>
/// Indonesian tax identity of a party.
/// NPWP: 15 digits (legacy) or 16 digits (NIK-based / new format since 2024).
/// NITKU: 22 digits (Nomor Identitas Tempat Kegiatan Usaha, used by Coretax).
/// PKP: registered VAT entrepreneur, required to issue tax invoices (faktur pajak).
/// </summary>
public sealed record TaxIdentity
{
    public static readonly TaxIdentity None = new(null, null, false);

    private TaxIdentity(string? npwp, string? nitku, bool isPkp)
    {
        Npwp = npwp;
        Nitku = nitku;
        IsPkp = isPkp;
    }

    public string? Npwp { get; private init; }

    public string? Nitku { get; private init; }

    public bool IsPkp { get; private init; }

    public static Result<TaxIdentity> Create(string? npwp, string? nitku, bool isPkp)
    {
        string? normalizedNpwp = Digits.Normalize(npwp);
        string? normalizedNitku = Digits.Normalize(nitku);

        if (normalizedNpwp is not null && normalizedNpwp.Length is not (15 or 16))
        {
            return Result.Failure<TaxIdentity>(CommonErrors.InvalidNpwp);
        }

        if (normalizedNitku is not null && normalizedNitku.Length != 22)
        {
            return Result.Failure<TaxIdentity>(CommonErrors.InvalidNitku);
        }

        if (isPkp && normalizedNpwp is null)
        {
            return Result.Failure<TaxIdentity>(CommonErrors.PkpRequiresNpwp);
        }

        return new TaxIdentity(normalizedNpwp, normalizedNitku, isPkp);
    }
}
