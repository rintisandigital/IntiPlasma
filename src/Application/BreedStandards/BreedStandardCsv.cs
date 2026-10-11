using System.Globalization;
using System.Text;
using Domain.MasterData.BreedStandards;
using SharedKernel;

namespace Application.BreedStandards;

/// <summary>
/// CSV of breed standard rows (PLAN-MOBILE M-73): header <c>age,bw,fcr,depletion,daily_feed,cum_feed</c> in any order
/// (only <c>age</c> and <c>bw</c> required), separated by <c>,</c> (decimal point) or <c>;</c> (decimal point or
/// comma). The same layout is
/// used for the export, so an exported file can be edited and imported again.
/// </summary>
public static class BreedStandardCsv
{
    public const string Header = "age,bw,fcr,depletion,daily_feed,cum_feed";

    private const int MaxErrors = 10;

    private static readonly string[] Columns = ["age", "bw", "fcr", "depletion", "daily_feed", "cum_feed"];

    public static Result<List<StandardPoint>> Parse(string csv)
    {
        string[] lines = csv.ReplaceLineEndings("\n").Split('\n');
        int headerIndex = Array.FindIndex(lines, l => !string.IsNullOrWhiteSpace(l));

        if (headerIndex < 0)
        {
            return Result.Failure<List<StandardPoint>>(BreedStandardErrors.NoRows);
        }

        char separator = lines[headerIndex].Contains(';', StringComparison.Ordinal) ? ';' : ',';
        string[] header = [.. lines[headerIndex].Split(separator).Select(h => h.Trim().Trim('"'))];
        int[] positions = [.. Columns.Select(c => Array.FindIndex(header, h => string.Equals(h, c, StringComparison.OrdinalIgnoreCase)))];

        if (positions[0] < 0 || positions[1] < 0)
        {
            return Result.Failure<List<StandardPoint>>(InvalidCsv($"the header must contain 'age' and 'bw' ({Header})"));
        }

        var points = new List<StandardPoint>();
        var errors = new List<string>();

        for (int i = headerIndex + 1; i < lines.Length && errors.Count < MaxErrors; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            string[] cells = lines[i].Split(separator);
            decimal?[] values = new decimal?[Columns.Length];
            string? error = null;

            for (int c = 0; c < Columns.Length && error is null; c++)
            {
                string cell = positions[c] >= 0 && positions[c] < cells.Length ? cells[positions[c]].Trim().Trim('"') : string.Empty;

                if (cell.Length == 0)
                {
                    continue;
                }

                // Excel with an Indonesian locale saves ';'-separated files with a decimal comma.
                if (separator == ';')
                {
                    cell = cell.Replace(',', '.');
                }

                if (decimal.TryParse(cell, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
                {
                    values[c] = value;
                }
                else
                {
                    error = $"line {i + 1}: '{cell}' in column {Columns[c]} is not a number";
                }
            }

            if (error is null && (values[0] is not { } age || age != decimal.Truncate(age) || values[1] is null))
            {
                error = $"line {i + 1}: age (whole days) and bw are required";
            }

            if (error is not null)
            {
                errors.Add(error);
                continue;
            }

            points.Add(new StandardPoint((int)values[0]!.Value, values[1]!.Value, values[2], values[3], values[4], values[5]));
        }

        if (errors.Count > 0)
        {
            return Result.Failure<List<StandardPoint>>(InvalidCsv(string.Join("; ", errors)));
        }

        return points.Count == 0 ? Result.Failure<List<StandardPoint>>(BreedStandardErrors.NoRows) : points;
    }

    public static string Write(IEnumerable<StandardPoint> rows)
    {
        StringBuilder csv = new StringBuilder().AppendLine(Header);

        foreach (StandardPoint row in rows)
        {
            csv.AppendLine(string.Join(',',
                row.AgeDays.ToString(CultureInfo.InvariantCulture),
                Format(row.BodyWeightGram),
                Format(row.Fcr),
                Format(row.DepletionPercent),
                Format(row.DailyFeedGram),
                Format(row.CumulativeFeedGram)));
        }

        return csv.ToString();
    }

    private static string Format(decimal? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;

    private static Error InvalidCsv(string detail) => Error.Problem("BreedStandards.InvalidCsv", $"The CSV is invalid: {detail}");
}
