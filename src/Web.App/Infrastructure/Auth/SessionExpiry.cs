using Microsoft.AspNetCore.Authentication;

namespace Web.App.Infrastructure.Auth;

/// <summary>
/// When the sign-in cookie of the current request expires (W10 session warning). The cookie has a sliding
/// lifetime: a request made after half of it has passed renews the cookie, so the expiry moves to now + lifetime.
/// </summary>
public static class SessionExpiry
{
    public static DateTimeOffset? Get(HttpContext context)
    {
        AuthenticationProperties? properties =
            context.Features.Get<IAuthenticateResultFeature>()?.AuthenticateResult?.Properties;

        if (properties?.ExpiresUtc is not { } expires || properties.IssuedUtc is not { } issued)
        {
            return null;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan lifetime = expires - issued;

        return now - issued > lifetime / 2 ? now + lifetime : expires;
    }

    /// <summary>
    /// Unix milliseconds for the <c>data-session-expires</c> attribute read by the mainboard script.
    /// </summary>
    public static string? ForScript(HttpContext context) =>
        Get(context)?.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
}
