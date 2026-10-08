namespace Domain.MasterData.Coops;

/// <summary>
/// Survey data of a coop (data kandang peternak): location details, building, equipment, production plan,
/// storage capacity and field notes. Descriptive only; no business rule depends on it. Stored as one JSON column.
/// </summary>
public sealed record CoopProfile
{
    // ---- Detail ----

    public string? Phone { get; init; }
    public string? Country { get; init; }
    public string? Province { get; init; }
    public string? City { get; init; }

    /// <summary>
    /// The company the farmer partnered with before (partner terakhir).
    /// </summary>
    public string? LastPartner { get; init; }

    // ---- Bangunan kandang ----

    public decimal? LengthM { get; init; }
    public decimal? WidthM { get; init; }
    public decimal? HeightM { get; init; }
    public int? Floors { get; init; }

    /// <summary>
    /// Konstruksi, e.g. wood, steel, bamboo.
    /// </summary>
    public string? Construction { get; init; }

    // ---- Peralatan kandang ----

    public CoopFan[] Fans { get; init; } = [];

    /// <summary>
    /// Wind speed measurements (running test) in m/s.
    /// </summary>
    public CoopFanTest[] FanTests { get; init; } = [];

    public int? NippleUnits { get; init; }

    /// <summary>
    /// Tempat minum ayam otomatis (bell drinkers).
    /// </summary>
    public int? TmaoUnits { get; init; }

    public int? ChickFeederUnits { get; init; }

    /// <summary>
    /// Tempat ransum ayam (hanging feeders).
    /// </summary>
    public int? TraUnits { get; init; }

    public int? SuperFeederUnits { get; init; }
    public int? AugerUnits { get; init; }

    /// <summary>
    /// Tirai, e.g. terpaulin.
    /// </summary>
    public string? Curtain { get; init; }

    public string? GensetBrand { get; init; }
    public decimal? GensetKva { get; init; }

    /// <summary>
    /// True when the genset is factory-made (pabrikan), false when assembled.
    /// </summary>
    public bool? GensetFactoryMade { get; init; }

    // ---- Rencana produksi ----

    public decimal? TonnageCapacityKg { get; init; }
    public decimal? InitialDensityBirdsPerM2 { get; init; }
    public decimal? InitialDensityKgPerM2 { get; init; }
    public decimal? FinalDensityBirdsPerM2 { get; init; }
    public decimal? FinalDensityKgPerM2 { get; init; }
    public CoopThinning[] Thinnings { get; init; } = [];

    // ---- Kapasitas gudang ----

    public decimal? FeedStorageKg { get; init; }
    public int? HuskStorageSacks { get; init; }
    public int? Heaters { get; init; }

    // ---- Lain-lain ----

    public LocationAccess? LocationAccess { get; init; }

    /// <summary>
    /// Notes about the local collector (pengepul).
    /// </summary>
    public string? CollectorNotes { get; init; }

    public string? FarmerNotes { get; init; }
    public string? OtherPartnerNotes { get; init; }
    public string? OtherNotes { get; init; }
    public string? Remarks { get; init; }

    /// <summary>
    /// Floor area (m²) = length × width × floors.
    /// </summary>
    public decimal? AreaM2() => LengthM * WidthM * (Floors ?? 1);

    /// <summary>
    /// Building volume (m³) = area × height.
    /// </summary>
    public decimal? VolumeM3() => AreaM2() * HeightM;

    /// <summary>
    /// Trimmed strings (empty → null) and without blank list rows.
    /// </summary>
    public CoopProfile Normalize() => this with
    {
        Phone = Clean(Phone),
        Country = Clean(Country),
        Province = Clean(Province),
        City = Clean(City),
        LastPartner = Clean(LastPartner),
        Construction = Clean(Construction),
        Fans = [.. (Fans ?? []).Where(f => f is not null && (Clean(f.Brand) is not null || f.Capacity is not null || f.Quantity is not null))
            .Select(f => f with { Brand = Clean(f.Brand) })],
        FanTests = [.. (FanTests ?? []).Where(t => t is not null && (t.FrontMs is not null || t.MiddleMs is not null || t.BackMs is not null))],
        Curtain = Clean(Curtain),
        GensetBrand = Clean(GensetBrand),
        Thinnings = [.. (Thinnings ?? []).Where(t => t is not null && (t.WeightKg is not null || t.Birds is not null))],
        CollectorNotes = Clean(CollectorNotes),
        FarmerNotes = Clean(FarmerNotes),
        OtherPartnerNotes = Clean(OtherPartnerNotes),
        OtherNotes = Clean(OtherNotes),
        Remarks = Clean(Remarks)
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CoopFan
{
    public string? Brand { get; init; }

    /// <summary>
    /// Fan capacity as written on the survey (e.g. 1.5 HP or 36 inch).
    /// </summary>
    public decimal? Capacity { get; init; }

    public int? Quantity { get; init; }
}

public sealed record CoopFanTest
{
    public decimal? FrontMs { get; init; }
    public decimal? MiddleMs { get; init; }
    public decimal? BackMs { get; init; }

    /// <summary>
    /// Average of the filled measurements.
    /// </summary>
    public decimal? AverageMs()
    {
        decimal[] values = [.. new[] { FrontMs, MiddleMs, BackMs }.OfType<decimal>()];
        return values.Length == 0 ? null : values.Average();
    }
}

/// <summary>
/// Planned thinning (penjarangan): birds taken out early at the given body weight.
/// </summary>
public sealed record CoopThinning
{
    public decimal? WeightKg { get; init; }
    public int? Birds { get; init; }
}

public enum LocationAccess
{
    Easy = 1,
    Middle = 2,
    Hard = 3
}
