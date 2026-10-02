using Application.Abstractions.Messaging;

namespace Application.Users.GetSession;

/// <summary>
/// The data a cookie session is re-validated against (Web.App): the session stays valid while the user is
/// active and the security stamp is unchanged.
/// </summary>
public sealed record GetUserSessionQuery(Guid UserId) : IQuery<UserSessionResponse>;
