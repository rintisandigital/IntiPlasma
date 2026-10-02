using Application.Abstractions.Messaging;

namespace Application.Users.GetCurrent;

/// <summary>
/// The signed-in user with the branches they may work in (for the Web.App header and branch switcher).
/// </summary>
public sealed record GetCurrentUserQuery : IQuery<CurrentUserResponse>;
