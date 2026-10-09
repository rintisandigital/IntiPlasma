using MobileApp.Core.Abstractions;

namespace MobileApp.Services;

/// <summary>
/// <see cref="ISecureStore"/> over MAUI <c>SecureStorage</c> (Android Keystore).
/// </summary>
internal sealed class MauiSecureStore : ISecureStore
{
    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch (Exception)
        {
            // The keystore can no longer decrypt the stored values (e.g. restored device): start without a session.
            SecureStorage.Default.RemoveAll();

            return null;
        }
    }

    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);

    public void Remove(string key) => SecureStorage.Default.Remove(key);
}

/// <summary>
/// <see cref="Core.Abstractions.IConnectivity"/> over MAUI <c>Connectivity</c>.
/// </summary>
internal sealed class MauiConnectivity : Core.Abstractions.IConnectivity, IDisposable
{
    public MauiConnectivity()
    {
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    public event EventHandler? Changed;

    public bool IsOnline => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public void Dispose() => Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// <see cref="IExternalLauncher"/> over MAUI <c>Launcher</c> (tel:, WhatsApp and map links).
/// </summary>
internal sealed class MauiExternalLauncher : IExternalLauncher
{
    public async Task<bool> OpenAsync(Uri uri)
    {
        try
        {
            return await Launcher.Default.OpenAsync(uri);
        }
        catch (InvalidOperationException)
        {
            // No app on the device handles this kind of link.
            return false;
        }
    }
}

/// <summary>
/// <see cref="IAppSettings"/> over MAUI <c>Preferences</c>.
/// </summary>
internal sealed class MauiAppSettings : IAppSettings
{
    private const string ServerUrlKey = "settings.server_url";
    private const string LastEmailKey = "settings.last_email";

    public string ServerUrl
    {
        get => CanEditServerUrl ? Preferences.Default.Get(ServerUrlKey, ServerDefaults.ServerUrl) : ServerDefaults.ServerUrl;
        set => Preferences.Default.Set(ServerUrlKey, value.Trim());
    }

    public string? LastEmail
    {
        get => Preferences.Default.Get<string?>(LastEmailKey, null);
        set => Preferences.Default.Set(LastEmailKey, value);
    }

#if DEBUG
    public bool CanEditServerUrl => true;
#else
    public bool CanEditServerUrl => false;
#endif
}

/// <summary>
/// Default Web.Api address per build. Debug talks to the API on the development machine (the Android emulator
/// reaches the host through 10.0.2.2). Release uses the production address configured here before publishing.
/// </summary>
internal static class ServerDefaults
{
#if DEBUG
    public static string ServerUrl =>
        DeviceInfo.Current.Platform == DevicePlatform.Android ? "http://10.0.2.2:5000/" : "http://localhost:5000/";
#else
    // TODO (M10): production address of Web.Api (HTTPS).
    public static string ServerUrl => "https://api.intiplasma.example/";
#endif
}
