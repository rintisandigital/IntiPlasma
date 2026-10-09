using System.Globalization;
using System.Threading.RateLimiting;
using Application.Abstractions.Authorization;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Auth;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;
using Web.App.Infrastructure.Forms;
using Web.App.Infrastructure.Formatting;
using Web.App.Infrastructure.Navigation;
using Web.App.Infrastructure.Web;
using Web.App.Infrastructure.Workflow;

namespace Web.App;

public static class DependencyInjection
{
    public const string AntiforgeryHeaderName = "X-CSRF-TOKEN";

    public const string LoginRateLimitPolicy = "login";

    /// <summary>
    /// Sliding lifetime of the sign-in cookie.
    /// </summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    public static IServiceCollection AddWebApp(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AppOptions>().Bind(configuration.GetSection(AppOptions.SectionName));

        // The PPL scope applies to the mobile API only; Web.App users work with Akses Menu & Akses Cabang
        // (PLAN-MOBILE §4.5).
        services.AddScoped<IFieldScope, UnrestrictedFieldScope>();

        services.AddControllersWithViews(options =>
        {
            // Every page requires a signed-in user unless marked [AllowAnonymous] (login, error pages).
            AuthorizationPolicy authenticated = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.Filters.Add(new AuthorizeFilter(authenticated));
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            options.Filters.Add<DbExceptionFilter>();
            options.Filters.Add<FormTokenFilter>();
            options.Filters.Add<ExportAuditFilter>();
        });

        SecurityOptions security = configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>()
            ?? new SecurityOptions();
        services.AddSingleton(security);

        CookieSecurePolicy securePolicy = security.SecureCookies
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;

        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryHeaderName;
            options.Cookie.SecurePolicy = securePolicy;
        });

        services.AddCookieAuthentication(securePolicy);

        // Keys for the cookies above live in PostgreSQL: sessions survive restarts and work across replicas (W10).
        services.AddSharedDataProtection("IntiPlasma.WebApp");

        services.AddLoginRateLimiter(security);

        if (security.TrustForwardedHeaders)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // The proxy is the container network's gateway, not a fixed address.
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
        }

        services.AddAppLocalization();

        services.AddScoped<IBranchContext, BranchContext>();
        services.AddScoped<IMainboardMenuProvider, DatabaseMainboardMenuProvider>();
        services.AddScoped<IMenuRights, MenuRightsService>();
        services.AddScoped<Areas.Admin.Controllers.AdminLookups>();
        services.AddHostedService<MenuCatalogSyncService>();
        services.AddScoped<IWorkflowActionService, DirectWorkflowActionService>();
        services.AddSingleton<DisplayFormatter>();

        // Export (W-3): Excel with ClosedXML, PDF with QuestPDF under the Community license (W-6).
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        services.AddOptions<ExportOptions>().Bind(configuration.GetSection(ExportOptions.SectionName));
        services.AddSingleton<PdfListExporter>();
        services.AddSingleton<ReportExporter>();
        services.AddScoped<ExportService>();
        services.AddScoped<PageSupport>();
        services.AddScoped<ItemOptions>();
        services.AddScoped<Areas.Inventory.InventoryOptions>();

        return services;
    }

    /// <summary>
    /// <c>POST /Auth/Login</c> is limited per client IP address (on top of the account lockout), W10.
    /// </summary>
    private static void AddLoginRateLimiter(this IServiceCollection services, SecurityOptions security)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, security.LoginPermitPerMinute),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });
    }

    private static void AddCookieAuthentication(this IServiceCollection services, CookieSecurePolicy securePolicy)
    {
        services.AddScoped<AppCookieEvents>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "ip.auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = securePolicy;
                options.ExpireTimeSpan = SessionLifetime;
                options.SlidingExpiration = true;
                options.LoginPath = "/Auth/Login";
                options.LogoutPath = "/Auth/Logout";
                options.AccessDeniedPath = "/Error/403";
                options.EventsType = typeof(AppCookieEvents);
            });

        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, MenuPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, MenuAuthorizationHandler>();
    }

    /// <summary>
    /// English UI (W-10). The request culture is en-US so model binding reads what HTML5 inputs post
    /// (<c>type="number"</c> → "1234.5", <c>type="date"</c> → "2026-10-03"); values are displayed in the
    /// Indonesian format (W-16) by <see cref="DisplayFormatter"/> and the exporters, which use
    /// <c>App:FormatCulture</c> explicitly.
    /// </summary>
    private static void AddAppLocalization(this IServiceCollection services)
    {
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var culture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo uiCulture = culture;

            options.DefaultRequestCulture = new RequestCulture(culture, uiCulture);
            options.SupportedCultures = [culture];
            options.SupportedUICultures = [uiCulture];
            options.RequestCultureProviders.Clear();
        });
    }
}
