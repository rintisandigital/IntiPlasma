namespace Application.Approvals;

internal static class UserNames
{
    /// <summary>
    /// The user's full name, or the e-mail address when no name is filled in.
    /// </summary>
    public static string Of(string? firstName, string? lastName, string email) =>
        $"{firstName} {lastName}".Trim() is { Length: > 0 } name ? name : email;
}
