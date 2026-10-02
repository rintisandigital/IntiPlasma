using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Web.App.Infrastructure;
using Web.App.Infrastructure.Auth;
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

        services.AddAppLocalization(configuration);

        services.AddScoped<IBranchContext, BranchContext>();
        services.AddScoped<IMainboardMenuProvider, StaticMainboardMenuProvider>();
        services.AddScoped<IWorkflowActionService, DirectWorkflowActionService>();
        services.AddSingleton<DisplayFormatter>();

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
    }

    /// <summary>
    /// English UI texts (W-10) with Indonesian number/date formatting (W-16) for every request.
    /// </summary>
    private static void AddAppLocalization(this IServiceCollection services, IConfiguration configuration)
    {
        string formatCulture = configuration[$"{AppOptions.SectionName}:{nameof(AppOptions.FormatCulture)}"] ?? "id-ID";

        services.Configure<RequestLocalizationOptions>(options =>
        {
            var culture = CultureInfo.GetCultureInfo(formatCulture);
            var uiCulture = CultureInfo.GetCultureInfo("en-US");

            options.DefaultRequestCulture = new RequestCulture(culture, uiCulture);
            options.SupportedCultures = [culture];
            options.SupportedUICultures = [uiCulture];
            options.RequestCultureProviders.Clear();
        });
    }
}
