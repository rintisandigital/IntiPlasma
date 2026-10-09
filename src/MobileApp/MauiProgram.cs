using Microsoft.Extensions.Logging;
using MobileApp.Core.Abstractions;
using MobileApp.Core.Api;
using MobileApp.Core.Local;
using MobileApp.Core.Production;
using MobileApp.Core.Session;
using MobileApp.Core.Sync;
using MobileApp.Services;

namespace MobileApp;

public static class MauiProgram
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        AddMobileServices(builder.Services);

        return builder.Build();
    }

    /// <summary>
    /// Platform services and MobileApp.Core (PLAN-MOBILE §3.1). One HttpClient per role: plain for the token
    /// refresh, with <see cref="AuthHandler"/> for every other API call.
    /// </summary>
    private static void AddMobileServices(IServiceCollection services)
    {
        services.AddSingleton<ISecureStore, MauiSecureStore>();
        services.AddSingleton<Core.Abstractions.IConnectivity, MauiConnectivity>();
        services.AddSingleton<IAppSettings, MauiAppSettings>();
        services.AddSingleton<IExternalLauncher, MauiExternalLauncher>();
        services.AddSingleton<IPhotoPicker, MauiPhotoPicker>();
        services.AddSingleton<IClock, JakartaClock>();

        services.AddSingleton(_ => new LocalDb(Path.Combine(FileSystem.AppDataDirectory, "intiplasma.db3")));
        services.AddSingleton<TokenStore>();
        services.AddSingleton(sp => new TokenRefresher(
            new HttpClient(new NetworkErrorHandler { InnerHandler = new HttpClientHandler() }) { Timeout = RequestTimeout },
            sp.GetRequiredService<IAppSettings>(),
            sp.GetRequiredService<TokenStore>()));

        services.AddSingleton(sp =>
        {
            var authHandler = new AuthHandler(sp.GetRequiredService<TokenStore>(), sp.GetRequiredService<TokenRefresher>())
            {
                InnerHandler = new NetworkErrorHandler { InnerHandler = new HttpClientHandler() }
            };

            return new ApiClient(new HttpClient(authHandler) { Timeout = RequestTimeout }, sp.GetRequiredService<IAppSettings>());
        });

        services.AddSingleton<UsersApi>();
        services.AddSingleton<ApiCache>();
        services.AddSingleton<PartnershipApi>();
        services.AddSingleton<SessionService>();

        // Daily recording offline (M2): the queue only sends for the user who is signed in.
        services.AddSingleton<ProductionApi>();
        services.AddSingleton(_ => new PendingFiles(Path.Combine(FileSystem.AppDataDirectory, "pending")));
        services.AddSingleton(sp => new SyncEngine(
            sp.GetRequiredService<ApiClient>(),
            sp.GetRequiredService<ProductionApi>(),
            sp.GetRequiredService<LocalDb>(),
            sp.GetRequiredService<PendingFiles>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<Core.Abstractions.IConnectivity>(),
            CurrentUserId(sp)));
        services.AddSingleton(sp => new RecordingService(
            sp.GetRequiredService<ProductionApi>(),
            sp.GetRequiredService<LocalDb>(),
            sp.GetRequiredService<SyncEngine>(),
            sp.GetRequiredService<PendingFiles>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<Core.Abstractions.IConnectivity>(),
            CurrentUserId(sp)));
        services.AddSingleton(sp => new StockService(
            sp.GetRequiredService<ProductionApi>(),
            sp.GetRequiredService<LocalDb>(),
            sp.GetRequiredService<SyncEngine>(),
            sp.GetRequiredService<RecordingService>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<Core.Abstractions.IConnectivity>(),
            CurrentUserId(sp)));

        // Dashboard & charts (M4).
        services.AddSingleton<DashboardService>();
    }

    private static Func<Guid?> CurrentUserId(IServiceProvider services)
    {
        var session = new Lazy<SessionService>(services.GetRequiredService<SessionService>);

        return () => session.Value is { IsSignedIn: true, User: { } user } ? user.Id : null;
    }
}
