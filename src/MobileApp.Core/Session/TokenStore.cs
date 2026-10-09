using MobileApp.Core.Abstractions;
using MobileApp.Core.Contracts;

namespace MobileApp.Core.Session;

/// <summary>
/// Access and refresh token of the signed-in user: kept in memory for every request and persisted in the device's
/// secure storage so the session survives restarts (also offline).
/// </summary>
public sealed class TokenStore(ISecureStore secureStore)
{
    internal const string AccessTokenKey = "auth.access_token";
    internal const string RefreshTokenKey = "auth.refresh_token";

    private readonly Lock _gate = new();
    private string? _accessToken;
    private string? _refreshToken;

    public string? AccessToken
    {
        get
        {
            lock (_gate)
            {
                return _accessToken;
            }
        }
    }

    public string? RefreshToken
    {
        get
        {
            lock (_gate)
            {
                return _refreshToken;
            }
        }
    }

    public bool HasSession => RefreshToken is not null;

    public async Task LoadAsync()
    {
        string? access = await secureStore.GetAsync(AccessTokenKey);
        string? refresh = await secureStore.GetAsync(RefreshTokenKey);

        lock (_gate)
        {
            _accessToken = access;
            _refreshToken = refresh;
        }
    }

    public async Task SaveAsync(AccessTokens tokens)
    {
        lock (_gate)
        {
            _accessToken = tokens.AccessToken;
            _refreshToken = tokens.RefreshToken;
        }

        await secureStore.SetAsync(AccessTokenKey, tokens.AccessToken);
        await secureStore.SetAsync(RefreshTokenKey, tokens.RefreshToken);
    }

    public void Clear()
    {
        lock (_gate)
        {
            _accessToken = null;
            _refreshToken = null;
        }

        secureStore.Remove(AccessTokenKey);
        secureStore.Remove(RefreshTokenKey);
    }
}
