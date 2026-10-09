using MobileApp.Core.Contracts;

namespace MobileApp.Core.Api;

/// <summary>
/// Sign-in and the signed-in user's own session (<c>users/*</c>).
/// </summary>
public sealed class UsersApi(ApiClient api)
{
    public Task<ApiResult<AccessTokens>> LoginAsync(string email, string password, CancellationToken cancellationToken = default) =>
        api.PostAsync<AccessTokens>("users/login", new { email, password }, cancellationToken);

    public Task<ApiResult<CurrentUser>> GetMeAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<CurrentUser>("users/me", cancellationToken);

    public Task<ApiResult> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        api.PostAsync("users/logout", new { refreshToken }, cancellationToken);

    public Task<ApiResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default) =>
        api.PostAsync("users/me/change-password", new { currentPassword, newPassword }, cancellationToken);
}
