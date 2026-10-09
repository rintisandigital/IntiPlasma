namespace Application.Users;

/// <summary>
/// Lifetime of API refresh tokens, bound from <c>Jwt:RefreshTokenExpirationInDays</c>. Each refresh rotates the
/// token and restarts the lifetime, so a mobile session stays signed in while it is used at least once per period
/// (PLAN-MOBILE M-17).
/// </summary>
public sealed class RefreshTokenOptions
{
    public const string SectionName = "Jwt";

    public int RefreshTokenExpirationInDays { get; set; } = 30;
}
