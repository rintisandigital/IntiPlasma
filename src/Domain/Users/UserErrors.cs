using SharedKernel;

namespace Domain.Users;

public static class UserErrors
{
    public static Error NotFound(Guid userId) => Error.NotFound(
        "Users.NotFound",
        $"The user with the Id = '{userId}' was not found");

    public static Error Unauthorized() => Error.Failure(
        "Users.Unauthorized",
        "You are not authorized to perform this action.");

    public static readonly Error NotFoundByEmail = Error.NotFound(
        "Users.NotFoundByEmail",
        "The user with the specified email was not found");

    public static readonly Error EmailNotUnique = Error.Conflict(
        "Users.EmailNotUnique",
        "The provided email is not unique");

    public static readonly Error InvalidCredentials = Error.Problem(
        "Users.InvalidCredentials",
        "The email or password is incorrect");

    public static readonly Error Inactive = Error.Problem(
        "Users.Inactive",
        "The user account is inactive");

    public static readonly Error InvalidCurrentPassword = Error.Problem(
        "Users.InvalidCurrentPassword",
        "The current password is incorrect");

    public static readonly Error NoMenuAccess = Error.Problem(
        "Users.NoMenuAccess",
        "No menu access has been assigned to this account; contact your administrator");

    public static readonly Error DefaultBranchNotAccessible = Error.Problem(
        "Users.DefaultBranchNotAccessible",
        "The default branch must be an active branch covered by the branch access profile");

    public static readonly Error CannotDeactivateSelf = Error.Problem(
        "Users.CannotDeactivateSelf",
        "You cannot deactivate your own account");

    public static readonly Error CannotDeleteSelf = Error.Problem(
        "Users.CannotDeleteSelf",
        "You cannot delete your own account");

    public static readonly Error LastAdministrator = Error.Conflict(
        "Users.LastAdministrator",
        "This is the last active user with Full Access; assign Full Access to another active user first");

    public static readonly Error InvalidRefreshToken = Error.Problem(
        "Users.InvalidRefreshToken",
        "The provided refresh token is invalid or has expired");
}
