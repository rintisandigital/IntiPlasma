using Application.Abstractions.Messaging;

namespace Application.Users.SignIn;

/// <summary>
/// Verifies credentials for a cookie session (Web.App). Unlike <c>LoginUserCommand</c> it issues no tokens.
/// </summary>
public sealed record SignInUserCommand(string Email, string Password) : ICommand<SignedInUserResponse>;
