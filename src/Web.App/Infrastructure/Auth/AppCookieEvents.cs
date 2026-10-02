using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Users.GetSession;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;
using Web.App.Infrastructure.Web;

namespace Web.App.Infrastructure.Auth;

/// <summary>
/// Re-validates every cookie session against the database (cached; the cache is invalidated across processes
/// when the user is deactivated or changes password), and answers AJAX requests with 401/403 instead of a
/// login redirect.
/// </summary>
internal sealed class AppCookieEvents(
    IQueryHandler<GetUserSessionQuery, UserSessionResponse> sessionQuery,
    HybridCache cache) : CookieAuthenticationEvents
{
    private static readonly HybridCacheEntryOptions CacheOptions = new() { Expiration = TimeSpan.FromMinutes(5) };
    private static readonly string[] CacheTags = [PermissionCacheKeys.Tag];

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        Guid? userId = context.Principal?.GetUserId();
        string? stamp = context.Principal?.GetSecurityStamp();

        if (userId is null || stamp is null)
        {
            await RejectAsync(context);
            return;
        }

        UserSessionResponse? session = await cache.GetOrCreateAsync(
            PermissionCacheKeys.SessionForUser(userId.Value),
            async token =>
            {
                Result<UserSessionResponse> result = await sessionQuery.Handle(new GetUserSessionQuery(userId.Value), token);
                return result.IsSuccess ? result.Value : null;
            },
            CacheOptions,
            CacheTags,
            context.HttpContext.RequestAborted);

        if (session is null || !session.IsActive || !string.Equals(session.SecurityStamp, stamp, StringComparison.Ordinal))
        {
            await RejectAsync(context);
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.IsAjax())
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        return base.RedirectToLogin(context);
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.IsAjax())
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        return base.RedirectToAccessDenied(context);
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();

        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
