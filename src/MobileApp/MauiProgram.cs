using Microsoft.Extensions.Logging;
using MobileApp.Core.Abstractions;
using MobileApp.Core.Api;
using MobileApp.Core.Local;
using MobileApp.Core.Session;
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
    }
}
