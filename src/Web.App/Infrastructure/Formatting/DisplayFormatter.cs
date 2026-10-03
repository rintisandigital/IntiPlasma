using System.Globalization;
using Microsoft.Extensions.Options;

namespace Web.App.Infrastructure.Formatting;

/// <summary>
/// Formats values for display (W-16): Indonesian number/date formats and the Asia/Jakarta time zone while the
/// UI text stays English. Injected into views as <c>Fmt</c>.
/// </summary>
public sealed class DisplayFormatter
{
    private readonly CultureInfo _culture;
    private readonly TimeZoneInfo _timeZone;

    public DisplayFormatter(IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _culture = CultureInfo.GetCultureInfo(options.Value.FormatCulture);
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
    }

    /// <summary>1.234.567,89</summary>
    public string Number(decimal value, int decimals = 2) =>
        value.ToString($"N{decimals.ToString(CultureInfo.InvariantCulture)}", _culture);

    /// <summary>1.234.567 (counts: birds, coops, rows)</summary>
    public string Number(int value) => value.ToString("N0", _culture);

    /// <summary>1.234.567</summary>
    public string Number(long value) => value.ToString("N0", _culture);

    /// <summary>Rp 1.234.567,89</summary>
    public string Money(decimal value) => $"Rp {Number(value)}";

    /// <summary>dd/MM/yyyy</summary>
    public string Date(DateOnly value) => value.ToString("dd/MM/yyyy", _culture);

    /// <summary>The current time in the configured time zone.</summary>
    public DateTime LocalNow() => TimeZoneInfo.ConvertTimeFromUtc(System.DateTime.UtcNow, _timeZone);

    /// <summary>Today in the configured time zone.</summary>
    public DateOnly Today() => DateOnly.FromDateTime(LocalNow());

    /// <summary>The UTC moment the given local day (configured time zone) starts.</summary>
    public DateTime StartOfDayUtc(DateOnly localDate) =>
        TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), _timeZone);

    /// <summary>A UTC timestamp in the configured time zone.</summary>
    public DateTime ToLocal(DateTime utcValue) =>
        TimeZoneInfo.ConvertTimeFromUtc(System.DateTime.SpecifyKind(utcValue, DateTimeKind.Utc), _timeZone);

    /// <summary>A UTC timestamp shown in the configured time zone: dd/MM/yyyy HH:mm.</summary>
    public string DateTime(DateTime utcValue) =>
        TimeZoneInfo.ConvertTimeFromUtc(System.DateTime.SpecifyKind(utcValue, DateTimeKind.Utc), _timeZone)
            .ToString("dd/MM/yyyy HH:mm", _culture);
}
