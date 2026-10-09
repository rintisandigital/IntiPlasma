namespace MobileApp.Services;

/// <summary>
/// The platform HTTP handler. On Android, <c>AndroidMessageHandler</c> throws Java exceptions
/// (<c>Java.Net.ConnectException</c>, <c>UnknownHostException</c>, <c>SocketTimeoutException</c>) when there is no
/// network; they are turned into <see cref="HttpRequestException"/>, which MobileApp.Core reports as "offline" (and
/// then shows cached data) instead of crashing the page.
/// </summary>
internal sealed class NetworkErrorHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
#if ANDROID
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (Java.IO.IOException exception)
        {
            throw new HttpRequestException(exception.Message, exception);
        }
#else
        return await base.SendAsync(request, cancellationToken);
#endif
    }
}
