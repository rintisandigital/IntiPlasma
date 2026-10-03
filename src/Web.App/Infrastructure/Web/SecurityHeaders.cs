using System.Security.Cryptography;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Web.App.Infrastructure.Web;

/// <summary>
/// Security settings of Web.App (section <c>Security</c>), W10.
/// </summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// Sends the page policy as <c>Content-Security-Policy-Report-Only</c> (violations are only logged).
    /// </summary>
    public bool CspReportOnly { get; set; }

    /// <summary>
    /// Cookies are only sent over HTTPS. Turn off only when the application is served over plain HTTP.
    /// </summary>
    public bool SecureCookies { get; set; } = true;

    /// <summary>
    /// Trust <c>X-Forwarded-For/Proto</c> from the reverse proxy in front of the container.
    /// </summary>
    public bool TrustForwardedHeaders { get; set; }

    /// <summary>
    /// Sign-in attempts per client IP address and minute.
    /// </summary>
    public int LoginPermitPerMinute { get; set; } = 10;
}

public static partial class SecurityHeaders
{
    public const string CspReportPath = "/csp-report";

    private const string NonceKey = "csp-nonce";

    /// <summary>
    /// The nonce that allows the inline <c>&lt;script&gt;</c> blocks of the current response.
    /// </summary>
    public static string? GetCspNonce(this HttpContext context) => context.Items[NonceKey] as string;

    /// <summary>
    /// Pages may only be framed by the application itself (the mainboard iframe, PLAN-WEBAPP §3.10). HTML pages
    /// get a Content-Security-Policy that only runs the application's own scripts and inline scripts carrying the
    /// per-request nonce (§23.3); files (PDF/photo previews, exports) keep only the framing rule.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, SecurityOptions options) =>
        app.Use(async (context, next) =>
        {
            // Hex: no characters that HTML attribute encoding would rewrite.
            string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            context.Items[NonceKey] = nonce;

            context.Response.OnStarting(() =>
            {
                IHeaderDictionary headers = context.Response.Headers;
                headers.XFrameOptions = "SAMEORIGIN";
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                headers.ContentSecurityPolicy = "frame-ancestors 'self'";

                bool html = context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true;
                if (html)
                {
                    headers[options.CspReportOnly ? "Content-Security-Policy-Report-Only" : "Content-Security-Policy"] =
                        PagePolicy(nonce);
                }

                return Task.CompletedTask;
            });

            await next(context);
        });

    /// <summary>
    /// Logs CSP violations reported by browsers (report-only mode and enforcement).
    /// </summary>
    public static IEndpointRouteBuilder MapCspReports(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(CspReportPath, async (HttpRequest request, ILoggerFactory loggerFactory) =>
        {
            using var reader = new StreamReader(request.Body);
            char[] buffer = new char[8192];
            int length = await reader.ReadBlockAsync(buffer);

            ILogger logger = loggerFactory.CreateLogger("Web.App.Csp");
            LogViolation(logger, new string(buffer, 0, length));

            return Microsoft.AspNetCore.Http.Results.NoContent();
        })
        .AllowAnonymous()
        .DisableAntiforgery();

        return endpoints;
    }

    internal static string PagePolicy(string nonce) =>
        string.Join("; ",
            "default-src 'self'",
            $"script-src 'self' 'nonce-{nonce}'",
            // style attributes and the styles injected by SweetAlert2, Tom-Select, ApexCharts.
            "style-src 'self' 'unsafe-inline'",
            "img-src 'self' data: blob:",
            "font-src 'self' data:",
            "connect-src 'self'",
            "frame-src 'self' blob:",
            "frame-ancestors 'self'",
            "form-action 'self'",
            "base-uri 'self'",
            "object-src 'none'",
            $"report-uri {CspReportPath}");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Content-Security-Policy violation: {Report}")]
    private static partial void LogViolation(ILogger logger, string report);
}

/// <summary>
/// Adds the response's CSP nonce to every <c>&lt;script&gt;</c> element, so the inline scripts of the views run
/// under the Content-Security-Policy.
/// </summary>
[HtmlTargetElement("script")]
public sealed class ScriptNonceTagHelper(IHttpContextAccessor httpContextAccessor) : TagHelper
{
    // After the built-in script tag helper (asp-append-version), which has order -1000.
    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        string? nonce = httpContextAccessor.HttpContext?.GetCspNonce();

        if (nonce is not null && !output.Attributes.ContainsName("nonce"))
        {
            output.Attributes.Add("nonce", nonce);
        }
    }
}
