using Application.Abstractions.Messaging;

namespace Application.Users.AssignBranches;

/// <summary>
/// Replaces the branches a user may access. Users with the <c>branches:access-all</c> permission see every branch regardless.
/// </summary>
public sealed record AssignUserBranchesCommand(Guid UserId, IReadOnlyList<Guid> BranchIds) : ICommand;
