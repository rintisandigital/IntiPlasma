namespace Web.App.Infrastructure.Web;

public static class SecurityHeaders
{
    /// <summary>
    /// Pages may only be framed by the application itself (the mainboard iframe), see PLAN-WEBAPP §3.10.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            IHeaderDictionary headers = context.Response.Headers;
            headers.XFrameOptions = "SAMEORIGIN";
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers.ContentSecurityPolicy = "frame-ancestors 'self'";

            await next(context);
        });
}
