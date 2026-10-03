using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
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

    public static IServiceCollection AddWebApp(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AppOptions>().Bind(configuration.GetSection(AppOptions.SectionName));

        services.AddControllersWithViews(options =>
        {
            // Every page requires a signed-in user unless marked [AllowAnonymous] (login, error pages).
            AuthorizationPolicy authenticated = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.Filters.Add(new AuthorizeFilter(authenticated));
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            options.Filters.Add<DbExceptionFilter>();
            options.Filters.Add<FormTokenFilter>();
        });

        services.AddAntiforgery(options => options.HeaderName = AntiforgeryHeaderName);

        services.AddCookieAuthentication();

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
        services.AddScoped<ExportService>();
        services.AddScoped<PageSupport>();

        return services;
    }

    private static void AddCookieAuthentication(this IServiceCollection services)
    {
        services.AddScoped<AppCookieEvents>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "ip.auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
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
