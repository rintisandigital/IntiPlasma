using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Web.App.Infrastructure.Auth;

/// <summary>
/// Builds and reads the cookie principal. The user id travels as <see cref="ClaimTypes.NameIdentifier"/>, the
/// claim Infrastructure's <c>UserContext</c> reads, so every use case sees the signed-in user.
/// </summary>
public static class UserPrincipal
{
    public const string SecurityStampClaim = "ip:security_stamp";

    public static ClaimsPrincipal Create(Guid userId, string email, string firstName, string lastName, string securityStamp)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, $"{firstName} {lastName}".Trim()),
                new Claim(ClaimTypes.Email, email),
                new Claim(SecurityStampClaim, securityStamp)
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// The same session with a renewed stamp (after the user changed their own password).
    /// </summary>
    public static ClaimsPrincipal WithSecurityStamp(this ClaimsPrincipal principal, string securityStamp)
    {
        var identity = new ClaimsIdentity(
            principal.Claims
                .Where(c => c.Type != SecurityStampClaim)
                .Select(c => new Claim(c.Type, c.Value))
                .Append(new Claim(SecurityStampClaim, securityStamp)),
            CookieAuthenticationDefaults.AuthenticationScheme);

        return new ClaimsPrincipal(identity);
    }

    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out Guid id)
            ? id
            : null;

    public static string GetDisplayName(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    public static string GetEmail(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public static string? GetSecurityStamp(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(SecurityStampClaim);

    /// <summary>
    /// Up to two initials for the avatar in the header.
    /// </summary>
    public static string GetInitials(this ClaimsPrincipal principal)
    {
        string[] parts = principal.GetDisplayName().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
    }
}
