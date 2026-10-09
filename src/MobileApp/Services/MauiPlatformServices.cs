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

/// <summary>
/// <see cref="IPhotoPicker"/> over MAUI <c>MediaPicker</c>: the picker itself turns the photo upright and reduces it
/// to 1600 px at JPEG quality 80 (PLAN-MOBILE M-44) before it is copied to the pending folder.
/// </summary>
internal sealed class MauiPhotoPicker : IPhotoPicker
{
    private static readonly MediaPickerOptions Options = new()
    {
        Title = "Foto recording",
        MaximumWidth = 1600,
        MaximumHeight = 1600,
        CompressionQuality = 80,
        RotateImage = true,
        PreserveMetaData = false
    };

    public Task<bool> CaptureAsync(string targetPath, CancellationToken cancellationToken = default) =>
        SaveAsync(() => MediaPicker.Default.CapturePhotoAsync(Options), targetPath, cancellationToken);

    public Task<bool> PickAsync(string targetPath, CancellationToken cancellationToken = default) =>
        SaveAsync(
            async () => (await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                Title = Options.Title,
                MaximumWidth = Options.MaximumWidth,
                MaximumHeight = Options.MaximumHeight,
                CompressionQuality = Options.CompressionQuality,
                RotateImage = Options.RotateImage,
                PreserveMetaData = Options.PreserveMetaData,
                SelectionLimit = 1
            }))?.FirstOrDefault(),
            targetPath,
            cancellationToken);

    private static async Task<bool> SaveAsync(Func<Task<FileResult?>> pick, string targetPath, CancellationToken cancellationToken)
    {
        FileResult? photo;
        try
        {
            photo = await MainThread.InvokeOnMainThreadAsync(pick);
        }
        catch (PermissionException)
        {
            return false;
        }
        catch (FeatureNotSupportedException)
        {
            return false;
        }

        if (photo is null)
        {
            return false;
        }

        await using Stream source = await photo.OpenReadAsync();
        await using FileStream target = File.Create(targetPath);
        await source.CopyToAsync(target, cancellationToken);

        return true;
    }
}
