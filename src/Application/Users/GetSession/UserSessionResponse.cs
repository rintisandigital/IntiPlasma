namespace Application.Users.GetSession;

public sealed record UserSessionResponse
{
    public bool IsActive { get; init; }

    public string SecurityStamp { get; init; }
}
