using System.Globalization;

namespace MobileApp.Core.Formatting;

/// <summary>
/// Links opened in other apps of the device: phone, WhatsApp chat and the map of a coop.
/// </summary>
public static class ExternalLinks
{
    private const string WhatsAppBase = "https://wa.me/";
    private const string MapsSearchBase = "https://www.google.com/maps/search/?api=1&query=";

    /// <summary>
    /// <c>tel:</c> link, or null without a usable number.
    /// </summary>
    public static Uri? Phone(string? phone) => Labels.TelNumber(phone) is { } number ? new Uri("tel:" + number) : null;

    /// <summary>
    /// WhatsApp chat with an (Indonesian) number, or null without a usable number.
    /// </summary>
    public static Uri? WhatsApp(string? phone) =>
        Labels.WhatsAppNumber(phone) is { } number ? new Uri(WhatsAppBase + number) : null;

    /// <summary>
    /// The location in the maps app (Google Maps universal link).
    /// </summary>
    public static Uri Map(decimal latitude, decimal longitude) =>
        new(MapsSearchBase + Uri.EscapeDataString(string.Create(CultureInfo.InvariantCulture, $"{latitude},{longitude}")));
}
