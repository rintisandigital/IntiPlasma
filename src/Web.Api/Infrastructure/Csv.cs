using System.Globalization;
using System.Text;

namespace Web.Api.Infrastructure;

/// <summary>
/// Minimal RFC 4180 CSV writer for report exports: comma separated, invariant numbers (dot decimals), ISO dates,
/// UTF-8 with BOM so spreadsheet applications detect the encoding.
/// </summary>
internal static class Csv
{
    public static IResult File<T>(string fileName, IEnumerable<T> rows, params (string Header, Func<T, object?> Value)[] columns)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', columns.Select(c => Escape(c.Header))));

        foreach (T row in rows)
        {
            builder.AppendLine(string.Join(',', columns.Select(c => Escape(Format(c.Value(row))))));
        }

        byte[] content = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];

        return Results.File(content, "text/csv", fileName);
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
        bool flag => flag ? "Y" : "N",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
}
