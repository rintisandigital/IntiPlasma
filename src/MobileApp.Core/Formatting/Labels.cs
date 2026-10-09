namespace MobileApp.Core.Formatting;

/// <summary>
/// Indonesian labels for the enum values Web.Api sends as strings (PLAN-MOBILE M-6), plus small display helpers.
/// Unknown values are shown as sent.
/// </summary>
public static class Labels
{
    /// <summary>
    /// Look of a status badge.
    /// </summary>
    public enum Tone
    {
        Neutral,
        Info,
        Success,
        Warning,
        Danger
    }

    public static string HouseType(string value) => value switch
    {
        "OpenHouse" => "Kandang terbuka",
        "ClosedHouse" => "Kandang tertutup",
        _ => value
    };

    public static string CycleStatus(string value) => value switch
    {
        "Planned" => "Direncanakan",
        "Active" => "Berjalan",
        "Harvesting" => "Panen",
        "Closed" => "Ditutup",
        "Settled" => "Selesai",
        "Cancelled" => "Dibatalkan",
        _ => value
    };

    public static Tone CycleTone(string value) => value switch
    {
        "Active" => Tone.Success,
        "Harvesting" => Tone.Warning,
        "Planned" => Tone.Info,
        "Cancelled" => Tone.Danger,
        _ => Tone.Neutral
    };

    /// <summary>
    /// Status of a daily recording on the device (PLAN-MOBILE §6.2): Terkirim / Belum terkirim / Gagal.
    /// </summary>
    public static string RecordingState(Production.RecordingState state) => state switch
    {
        Production.RecordingState.Sent => "Terkirim",
        Production.RecordingState.Sending => "Mengirim…",
        Production.RecordingState.Failed => "Gagal",
        _ => "Belum terkirim"
    };

    public static Tone RecordingTone(Production.RecordingState state) => state switch
    {
        Production.RecordingState.Sent => Tone.Success,
        Production.RecordingState.Failed => Tone.Danger,
        Production.RecordingState.Sending => Tone.Info,
        _ => Tone.Warning
    };

    public static string ContractStatus(string value) => value switch
    {
        "Draft" => "Draf",
        "Active" => "Aktif",
        "Inactive" => "Nonaktif",
        _ => value
    };

    public static Tone ContractTone(string value) => value switch
    {
        "Active" => Tone.Success,
        "Draft" => Tone.Info,
        _ => Tone.Neutral
    };

    public static string Scheme(string value) => value switch
    {
        "PriceContract" => "Harga kontrak",
        "ProfitSharing" => "Bagi hasil",
        _ => value
    };

    public static string IncentiveKind(string value) => value switch
    {
        "Bonus" => "Bonus",
        "Deduction" => "Potongan",
        _ => value
    };

    public static string IncentiveMetric(string value) => value switch
    {
        "None" => "Tanpa syarat",
        "Fcr" => "FCR",
        "Ip" => "IP",
        "Depletion" => "Deplesi (%)",
        "AverageWeight" => "Bobot rata-rata (kg)",
        _ => value
    };

    public static string IncentiveBasis(string value) => value switch
    {
        "PerKg" => "per kg",
        "PerBird" => "per ekor",
        "Fixed" => "per siklus",
        _ => value
    };

    public static string Active(bool isActive) => isActive ? "Aktif" : "Nonaktif";

    /// <summary>
    /// NIK with the middle digits hidden (PLAN-MOBILE §3.5), e.g. <c>3201********0001</c>.
    /// </summary>
    public static string MaskNik(string? nik)
    {
        if (string.IsNullOrWhiteSpace(nik))
        {
            return "—";
        }

        string value = nik.Trim();

        return value.Length <= 8 ? new string('*', value.Length) : $"{value[..4]}{new string('*', value.Length - 8)}{value[^4..]}";
    }

    /// <summary>
    /// The phone number for a <c>tel:</c> link (digits and a leading +).
    /// </summary>
    public static string? TelNumber(string? phone)
    {
        string digits = new([.. (phone ?? string.Empty).Where(char.IsDigit)]);

        if (digits.Length == 0)
        {
            return null;
        }

        return phone!.TrimStart().StartsWith('+') ? "+" + digits : digits;
    }

    /// <summary>
    /// The number for <c>https://wa.me/{number}</c>: Indonesian numbers in international form (08… → 628…).
    /// </summary>
    public static string? WhatsAppNumber(string? phone)
    {
        string digits = new([.. (phone ?? string.Empty).Where(char.IsDigit)]);

        if (digits.Length < 8)
        {
            return null;
        }

        return digits.StartsWith('0') ? "62" + digits[1..] : digits;
    }
}
