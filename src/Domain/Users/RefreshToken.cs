using SharedKernel;

namespace Domain.Users;

public sealed class RefreshToken : Entity
{
    private RefreshToken(Guid id, string token, Guid userId, DateTime expiresOnUtc)
        : base(id)
    {
        Token = token;
        UserId = userId;
        ExpiresOnUtc = expiresOnUtc;
    }

    private RefreshToken()
    {
    }

    public string Token { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime ExpiresOnUtc { get; private set; }
#pragma warning disable S1144 // Set by EF Core.
    public User User { get; private set; }
#pragma warning restore S1144

    public static RefreshToken Create(string token, Guid userId, DateTime expiresOnUtc) =>
        new(Guid.CreateVersion7(), token, userId, expiresOnUtc);

    public bool IsExpired(DateTime utcNow) => ExpiresOnUtc < utcNow;

    /// <summary>
    /// Rotates the token so a stolen refresh token can only be used once.
    /// </summary>
    public void Rotate(string newToken, DateTime expiresOnUtc)
    {
        Token = newToken;
        ExpiresOnUtc = expiresOnUtc;
    }
}
