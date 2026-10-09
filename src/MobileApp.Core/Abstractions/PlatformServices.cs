namespace MobileApp.Core.Abstractions;

/// <summary>
/// Encrypted key/value storage of the device (MAUI <c>SecureStorage</c>), used for the tokens.
/// </summary>
public interface ISecureStore
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    void Remove(string key);
}

/// <summary>
/// Network state of the device (MAUI <c>Connectivity</c>).
/// </summary>
public interface IConnectivity
{
    bool IsOnline { get; }

    event EventHandler? Changed;
}

/// <summary>
/// Plain preferences of the app (MAUI <c>Preferences</c>): not secret, survive restarts.
/// </summary>
public interface IAppSettings
{
    /// <summary>
    /// Base address of Web.Api, e.g. <c>https://api.example.co.id/</c> (the <c>api/v1/</c> part is added by the client).
    /// </summary>
    string ServerUrl { get; set; }

    /// <summary>
    /// Email of the last successful sign-in, prefilled on the login page.
    /// </summary>
    string? LastEmail { get; set; }

    /// <summary>
    /// Whether the server address may be changed in the settings page (debug builds only).
    /// </summary>
    bool CanEditServerUrl { get; }
}

/// <summary>
/// Opens a link in another app of the device (MAUI <c>Launcher</c>): phone, WhatsApp, maps.
/// </summary>
public interface IExternalLauncher
{
    Task<bool> OpenAsync(Uri uri);
}

/// <summary>
/// Current time. Dates of daily entries are local dates in <c>Asia/Jakarta</c> (PLAN-MOBILE M-6, §10).
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }

    DateOnly Today { get; }
}

/// <summary>
/// Photo from the camera or the gallery (MAUI <c>MediaPicker</c>), reduced to a JPEG of at most 1600 px on the
/// longest side (PLAN-MOBILE M-44) and written to <paramref name="targetPath"/>.
/// </summary>
public interface IPhotoPicker
{
    /// <returns>False when the user cancelled or the photo could not be read.</returns>
    Task<bool> CaptureAsync(string targetPath, CancellationToken cancellationToken = default);

    /// <returns>False when the user cancelled or the photo could not be read.</returns>
    Task<bool> PickAsync(string targetPath, CancellationToken cancellationToken = default);
}
