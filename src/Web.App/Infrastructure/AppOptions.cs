namespace Web.App.Infrastructure;

/// <summary>
/// Branding and formatting of the admin web application ("App" section).
/// </summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    public string Name { get; init; } = "Inti-Plasma";

    public string CompanyName { get; init; } = string.Empty;

    /// <summary>
    /// Culture used to format and parse numbers and dates (W-16). UI texts stay in English.
    /// </summary>
    public string FormatCulture { get; init; } = "id-ID";

    public string TimeZone { get; init; } = "Asia/Jakarta";
}
