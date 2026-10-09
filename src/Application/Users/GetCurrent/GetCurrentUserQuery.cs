using Application.Abstractions.Messaging;

namespace Application.Users.GetCurrent;

/// <summary>
/// The signed-in user with the branches they may work in (for the Web.App header and branch switcher) and their API
/// roles and permissions (<c>GET users/me</c> for the mobile app).
/// </summary>
public sealed record GetCurrentUserQuery : IQuery<CurrentUserResponse>;
