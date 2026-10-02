using System.Globalization;
using Application.Abstractions.Messaging;
using Application.Users.GetCurrent;
using SharedKernel;

namespace Web.App.Infrastructure.Auth;

/// <summary>
/// The signed-in user (with their branches) and the branch selected in the header. The selection is a
/// convenience filter only; branch access itself is enforced by the use cases.
/// </summary>
public interface IBranchContext
{
    /// <summary>
    /// The current user, loaded once per request.
    /// </summary>
    Task<CurrentUserResponse?> GetUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The selected branch, or null for "all branches" (only possible for users who may access every branch).
    /// A stale selection (branch no longer accessible) falls back to the first accessible branch.
    /// </summary>
    Task<CurrentUserBranch?> GetActiveBranchAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects a branch (null = all branches). Returns false when the user may not select it.
    /// </summary>
    Task<bool> SelectAsync(Guid? branchId, CancellationToken cancellationToken = default);

    void Clear();
}

internal sealed class BranchContext(
    IHttpContextAccessor httpContextAccessor,
    IQueryHandler<GetCurrentUserQuery, CurrentUserResponse> currentUserQuery) : IBranchContext
{
    public const string CookieName = "ip.branch";
    private const string AllBranchesValue = "all";

    private CurrentUserResponse? _user;
    private bool _loaded;

    private HttpContext HttpContext => httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("BranchContext requires an HTTP request.");

    public async Task<CurrentUserResponse?> GetUserAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            return _user;
        }

        Result<CurrentUserResponse> result = await currentUserQuery.Handle(new GetCurrentUserQuery(), cancellationToken);
        _user = result.IsSuccess ? result.Value : null;
        _loaded = true;

        return _user;
    }

    public async Task<CurrentUserBranch?> GetActiveBranchAsync(CancellationToken cancellationToken = default)
    {
        CurrentUserResponse? user = await GetUserAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        string? selected = HttpContext.Request.Cookies[CookieName];

        if (user.AllBranches && string.Equals(selected, AllBranchesValue, StringComparison.Ordinal))
        {
            return null;
        }

        if (Guid.TryParse(selected, CultureInfo.InvariantCulture, out Guid branchId) &&
            user.Branches.FirstOrDefault(b => b.Id == branchId) is { } branch)
        {
            return branch;
        }

        return user.Branches.Count > 0 ? user.Branches[0] : null;
    }

    public async Task<bool> SelectAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        CurrentUserResponse? user = await GetUserAsync(cancellationToken);
        if (user is null)
        {
            return false;
        }

        bool allowed = branchId is null ? user.AllBranches : user.Branches.Any(b => b.Id == branchId);
        if (!allowed)
        {
            return false;
        }

        HttpContext.Response.Cookies.Append(
            CookieName,
            branchId?.ToString() ?? AllBranchesValue,
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = HttpContext.Request.IsHttps,
                MaxAge = TimeSpan.FromDays(30)
            });

        return true;
    }

    public void Clear() => HttpContext.Response.Cookies.Delete(CookieName);
}
