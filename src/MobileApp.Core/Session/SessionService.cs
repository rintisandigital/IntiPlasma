using System.Text.Json;
using MobileApp.Core.Abstractions;
using MobileApp.Core.Api;
using MobileApp.Core.Contracts;
using MobileApp.Core.Local;

namespace MobileApp.Core.Session;

public enum SessionState
{
    /// <summary>
    /// <see cref="SessionService.InitializeAsync"/> has not finished yet.
    /// </summary>
    Starting,
    SignedOut,
    SignedIn
}

/// <summary>
/// Sign-in state of the app (PLAN-MOBILE §3.3). The profile and permissions from <c>users/me</c> are cached in the
/// local database, so a user with a stored session can open the app and work offline.
/// </summary>
public sealed class SessionService
{
    internal const string CurrentUserKey = "current_user";

    private readonly UsersApi _users;
    private readonly TokenStore _tokens;
    private readonly LocalDb _db;
    private readonly IAppSettings _settings;

    public SessionService(UsersApi users, TokenStore tokens, TokenRefresher refresher, LocalDb db, IAppSettings settings)
    {
        _users = users;
        _tokens = tokens;
        _db = db;
        _settings = settings;

        refresher.SessionExpired += (_, _) => SetState(SessionState.SignedOut, user: User, expired: true);
    }

    public SessionState State { get; private set; } = SessionState.Starting;

    /// <summary>
    /// The signed-in user (also kept after the session expired, to prefill the login page).
    /// </summary>
    public CurrentUser? User { get; private set; }

    /// <summary>
    /// True when the last sign-out happened because the server ended the session.
    /// </summary>
    public bool SessionExpired { get; private set; }

    public bool IsSignedIn => State == SessionState.SignedIn;

    /// <summary>
    /// Raised on every change of <see cref="State"/> or <see cref="User"/>; may come from a background thread.
    /// </summary>
    public event EventHandler? Changed;

    public bool Has(string permission) => User?.Has(permission) == true;

    /// <summary>
    /// Restores a stored session at start-up (works offline), then refreshes the profile when the server answers.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _db.InitializeAsync(cancellationToken);
        await _tokens.LoadAsync();

        CurrentUser? cached = await ReadCachedUserAsync(cancellationToken);

        if (_tokens.HasSession && cached is not null)
        {
            SetState(SessionState.SignedIn, cached);
            _ = RefreshProfileAsync(CancellationToken.None);
        }
        else
        {
            _tokens.Clear();
            SetState(SessionState.SignedOut, null);
        }
    }

    public async Task<ApiResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        _tokens.Clear();

        ApiResult<AccessTokens> login = await _users.LoginAsync(email.Trim(), password, cancellationToken);
        if (!login.IsSuccess)
        {
            return login;
        }

        await _tokens.SaveAsync(login.Value);

        ApiResult<CurrentUser> me = await _users.GetMeAsync(cancellationToken);
        if (!me.IsSuccess)
        {
            _tokens.Clear();

            return me;
        }

        if (me.Value.Permissions.Count == 0)
        {
            await _users.LogoutAsync(login.Value.RefreshToken, cancellationToken);
            _tokens.Clear();

            return ApiResult.Failure(new ApiError
            {
                Status = 403,
                Code = "Client.NoMobileAccess",
                Message = "Akun Anda belum diberi akses aplikasi mobile. Hubungi administrator."
            });
        }

        CurrentUser? previous = await ReadCachedUserAsync(cancellationToken);
        if (previous is not null && previous.Id != me.Value.Id)
        {
            // Another user signs in on this device: nothing of the previous user may stay visible.
            await _db.ClearUserDataAsync(cancellationToken);
        }

        await SaveCachedUserAsync(me.Value, cancellationToken);
        _settings.LastEmail = me.Value.Email;
        SetState(SessionState.SignedIn, me.Value);

        return ApiResult.Success();
    }

    /// <summary>
    /// Reloads profile and permissions (e.g. after an administrator changed the roles). Failures keep the cached
    /// profile; only an ended session signs the user out (through <see cref="TokenRefresher.SessionExpired"/>).
    /// </summary>
    public async Task<ApiResult> RefreshProfileAsync(CancellationToken cancellationToken = default)
    {
        ApiResult<CurrentUser> me = await _users.GetMeAsync(cancellationToken);
        if (!me.IsSuccess)
        {
            return me;
        }

        await SaveCachedUserAsync(me.Value, cancellationToken);
        if (IsSignedIn)
        {
            SetState(SessionState.SignedIn, me.Value);
        }

        return ApiResult.Success();
    }

    /// <summary>
    /// Ends the session on this device. The server is told when reachable; the device forgets the session anyway.
    /// </summary>
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        string? refreshToken = _tokens.RefreshToken;
        if (refreshToken is not null)
        {
            await _users.LogoutAsync(refreshToken, cancellationToken);
        }

        _tokens.Clear();
        await _db.ClearUserDataAsync(cancellationToken);
        SetState(SessionState.SignedOut, null);
    }

    /// <summary>
    /// Changes the password. The server ends every session of the user, so this device signs in again with the new
    /// password right away.
    /// </summary>
    public async Task<ApiResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ApiResult changed = await _users.ChangePasswordAsync(currentPassword, newPassword, cancellationToken);
        if (!changed.IsSuccess || User is null)
        {
            return changed;
        }

        ApiResult signedIn = await LoginAsync(User.Email, newPassword, cancellationToken);
        if (!signedIn.IsSuccess)
        {
            // The password did change, but the old session is gone: the user signs in with the new password.
            SetState(SessionState.SignedOut, User, expired: true);
        }

        return ApiResult.Success();
    }

    private async Task<CurrentUser?> ReadCachedUserAsync(CancellationToken cancellationToken)
    {
        string? json = await _db.GetValueAsync(CurrentUserKey, cancellationToken);

        return json is null ? null : JsonSerializer.Deserialize<CurrentUser>(json, ApiClient.JsonOptions);
    }

    private Task SaveCachedUserAsync(CurrentUser user, CancellationToken cancellationToken) =>
        _db.SetValueAsync(CurrentUserKey, JsonSerializer.Serialize(user, ApiClient.JsonOptions), cancellationToken);

    private void SetState(SessionState state, CurrentUser? user, bool expired = false)
    {
        State = state;
        User = user;
        SessionExpired = expired;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
