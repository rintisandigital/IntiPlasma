using System.Net;
using System.Net.Http.Headers;
using MobileApp.Core.Session;

namespace MobileApp.Core.Api;

/// <summary>
/// Adds the bearer token to every request and, when the access token has expired (401), refreshes it once and sends
/// the request again (PLAN-MOBILE §3.3).
/// </summary>
public sealed class AuthHandler(TokenStore tokens, TokenRefresher refresher) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? accessToken = tokens.AccessToken;
        if (accessToken is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // Buffer the body so the request can be sent a second time after a refresh.
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync(cancellationToken);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        RefreshOutcome outcome = await refresher.RefreshAsync(accessToken, cancellationToken);
        if (outcome != RefreshOutcome.Refreshed || tokens.AccessToken is not { } renewed)
        {
            return response;
        }

        response.Dispose();

        using HttpRequestMessage retry = await CloneAsync(request, cancellationToken);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", renewed);

        return await base.SendAsync(retry, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };

        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            byte[] body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var content = new ByteArrayContent(body);

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = content;
        }

        return clone;
    }
}
