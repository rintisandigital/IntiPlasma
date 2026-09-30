namespace Domain.Common;

internal static class Codes
{
    /// <summary>
    /// Business codes are case-insensitive identifiers; they are stored trimmed and upper-case.
    /// </summary>
    public static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
