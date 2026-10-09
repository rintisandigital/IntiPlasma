using System.Globalization;

namespace MobileApp.Core.Formatting;

/// <summary>
/// Indonesian display formats (PLAN-MOBILE M-6): <c>1.234.567,89</c>, <c>dd/MM/yyyy</c>, <c>Rp</c>. Built on the
/// invariant culture with explicit separators and names, so it does not depend on the device's locale data.
/// </summary>
public static class IdFormat
{
    private static readonly string[] Months =
    [
        "Januari", "Februari", "Maret", "April", "Mei", "Juni",
        "Juli", "Agustus", "September", "Oktober", "November", "Desember"
    ];

    private static readonly string[] Days = ["Minggu", "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu"];

    private static readonly NumberFormatInfo Numbers = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NegativeSign = "-"
    };

    public static string Number(decimal value, int decimals = 0) =>
        value.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), Numbers);

    public static string Number(int value) => Number((decimal)value);

    /// <summary>
    /// A figure that may be unknown (no body weight yet): "–" then.
    /// </summary>
    public static string Optional(decimal? value, int decimals = 0) => value is { } v ? Number(v, decimals) : "–";

    /// <summary>
    /// Feed stock in kg, plus packs when every item has the same largest unit, e.g. <c>900 kg (18 SAK)</c>.
    /// </summary>
    public static string Feed(decimal kilograms, IReadOnlyCollection<Contracts.DashboardFeedStock> items)
    {
        string kg = $"{Number(kilograms)} kg";
        List<string?> packs = [.. items.Select(i => i.PackUomCode).Distinct()];

        if (items.Count == 0 || packs.Count != 1 || packs[0] is not { } pack || items.Any(i => i.PackFactor is not > 0))
        {
            return kg;
        }

        return $"{kg} ({Number(items.Sum(i => i.Quantity / i.PackFactor!.Value), 1)} {pack})";
    }

    public static string Money(decimal value) => value < 0 ? $"-Rp {Number(-value)}" : $"Rp {Number(value)}";

    /// <summary>
    /// Kilograms shown as tons with two decimals, e.g. 12.345 kg → <c>12,35 ton</c>.
    /// </summary>
    public static string Tons(decimal kilograms) => $"{Number(kilograms / 1000m, 2)} ton";

    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Date(DateTime value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string DateTime(DateTime value) => value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// e.g. <c>Kamis, 8 Oktober 2026</c>.
    /// </summary>
    public static string LongDate(DateOnly date) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Days[(int)date.DayOfWeek]}, {date.Day} {Months[date.Month - 1]} {date.Year}");

    /// <summary>
    /// Greeting by the hour of the local time: pagi, siang, sore, malam.
    /// </summary>
    public static string Greeting(DateTime localTime) => localTime.Hour switch
    {
        < 11 => "Selamat pagi",
        < 15 => "Selamat siang",
        < 18 => "Selamat sore",
        _ => "Selamat malam"
    };
}
