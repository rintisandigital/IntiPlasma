namespace Web.App.Infrastructure.Web;

public static class HttpRequestExtensions
{
    /// <summary>
    /// True for fetch/XHR calls made by the pages (they send <c>X-Requested-With: XMLHttpRequest</c>).
    /// </summary>
    public static bool IsAjax(this HttpRequest request) =>
        string.Equals(request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.Ordinal);
}
