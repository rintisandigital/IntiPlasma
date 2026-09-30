namespace Domain.Common;

internal static class Digits
{
    /// <summary>
    /// Strips separators (dots, dashes, spaces) from identity numbers such as NPWP, NIK or account numbers.
    /// Returns null for empty input and the raw input when it contains other characters, so length checks fail.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string stripped = new([.. value.Where(c => c is not ('.' or '-' or ' '))]);

        return stripped.All(char.IsAsciiDigit) ? stripped : value;
    }

    public static bool IsDigits(string? value, int length) =>
        value is not null && value.Length == length && value.All(char.IsAsciiDigit);
}
