using Application.Abstractions.Messaging;

namespace Application.Users.ChangePassword;

/// <summary>
/// The signed-in user changes their own password. Other sessions and refresh tokens stop working.
/// </summary>
public sealed record ChangeOwnPasswordCommand(string CurrentPassword, string NewPassword) : ICommand;
