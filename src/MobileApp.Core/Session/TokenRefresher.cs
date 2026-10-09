using System.Net;
using System.Net.Http.Json;
using MobileApp.Core.Abstractions;
using MobileApp.Core.Api;
using MobileApp.Core.Contracts;

namespace MobileApp.Core.Session;

public enum RefreshOutcome
{
    /// <summary>
    /// New tokens are stored; the failed request can be sent again.
    /// </summary>
    Refreshed,

    /// <summary>
    /// The server rejected the refresh token: the session is over and the tokens were cleared.
    /// </summary>
    Rejected
}

/// <summary>
/// Exchanges the refresh token for new tokens. Refresh tokens rotate on every use, so only one refresh may run at a
/// time: concurrent requests that got a 401 wait, then reuse the tokens obtained by the first one.
/// </summary>
public sealed class TokenRefresher(HttpClient httpClient, IAppSettings settings, TokenStore tokens) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Raised once when the server rejects the refresh token (the user must sign in again).
    /// </summary>
    public event EventHandler? SessionExpired;

    /// <summary>
    /// Refreshes after a request made with <paramref name="rejectedAccessToken"/> got a 401. Throws
    /// <see cref="HttpRequestException"/> when the server cannot be reached, so the caller reports a network error
    /// instead of ending the session.
    /// </summary>
    public async Task<RefreshOutcome> RefreshAsync(string? rejectedAccessToken, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            string? current = tokens.AccessToken;
            if (current is not null && current != rejectedAccessToken)
            {
                // Another request refreshed while this one was waiting.
                return RefreshOutcome.Refreshed;
            }

            string? refreshToken = tokens.RefreshToken;
            if (refreshToken is null)
            {
                return Reject();
            }

            using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
                ApiClient.BuildUri(settings.ServerUrl, "users/refresh-token"),
                new { refreshToken },
                ApiClient.JsonOptions,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                AccessTokens? renewed = await response.Content.ReadFromJsonAsync<AccessTokens>(
                    ApiClient.JsonOptions,
                    cancellationToken);

                if (renewed is not null)
                {
                    await tokens.SaveAsync(renewed);

                    return RefreshOutcome.Refreshed;
                }
            }

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                // Invalid/expired refresh token or inactive account.
                return Reject();
            }

            throw new HttpRequestException(
                $"Refreshing the session failed with status {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private RefreshOutcome Reject()
    {
        bool hadSession = tokens.HasSession;
        tokens.Clear();

        if (hadSession)
        {
            SessionExpired?.Invoke(this, EventArgs.Empty);
        }

        return RefreshOutcome.Rejected;
    }
}
