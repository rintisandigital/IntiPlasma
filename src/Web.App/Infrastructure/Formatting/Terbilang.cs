using System.Globalization;
using System.Text;

namespace Web.App.Infrastructure.Formatting;

/// <summary>
/// Amount in words in Indonesian (terbilang) for printed invoices, receipts, payment vouchers and settlements (W-17):
/// 1.250.500,75 → "Satu juta dua ratus lima puluh ribu lima ratus rupiah tujuh puluh lima sen".
/// </summary>
public static class Terbilang
{
    private static readonly string[] Units =
        ["", "satu", "dua", "tiga", "empat", "lima", "enam", "tujuh", "delapan", "sembilan", "sepuluh", "sebelas"];

    private static readonly (decimal Scale, string Name)[] Scales =
    [
        (1_000_000_000_000m, "triliun"),
        (1_000_000_000m, "miliar"),
        (1_000_000m, "juta"),
        (1_000m, "ribu")
    ];

    public static string Rupiah(decimal amount)
    {
        decimal value = Math.Abs(Math.Round(amount, 2, MidpointRounding.AwayFromZero));
        decimal whole = Math.Floor(value);
        int cents = (int)((value - whole) * 100);

        var text = new StringBuilder(whole == 0 ? "nol" : Words(whole));
        text.Append(" rupiah");
        if (cents > 0)
        {
            text.Append(' ').Append(Words(cents)).Append(" sen");
        }

        string result = (amount < 0 ? "minus " : string.Empty) + text;
        return char.ToUpper(result[0], CultureInfo.InvariantCulture) + result[1..];
    }

    /// <summary>
    /// Words of a whole number from 1 to 999 trillion.
    /// </summary>
    public static string Words(decimal number)
    {
        if (number < 0 || number >= 1_000_000_000_000_000m || number != Math.Floor(number))
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "Only whole numbers below 10^15 are supported.");
        }

        var parts = new List<string>();
        decimal rest = number;

        foreach ((decimal scale, string name) in Scales)
        {
            int count = (int)Math.Floor(rest / scale);
            if (count == 0)
            {
                continue;
            }

            // 1.000 is "seribu", not "satu ribu"
            parts.Add(count == 1 && scale == 1_000m ? "seribu" : $"{Hundreds(count)} {name}");
            rest -= count * scale;
        }

        if (rest > 0)
        {
            parts.Add(Hundreds((int)rest));
        }

        return string.Join(' ', parts);
    }

    private static string Hundreds(int number)
    {
        int hundreds = number / 100;
        int rest = number % 100;
        string head = hundreds switch
        {
            0 => string.Empty,
            1 => "seratus",
            _ => $"{Units[hundreds]} ratus"
        };

        string tail = rest switch
        {
            0 => string.Empty,
            < 12 => Units[rest],
            < 20 => $"{Units[rest - 10]} belas",
            _ => $"{Units[rest / 10]} puluh{(rest % 10 == 0 ? string.Empty : " " + Units[rest % 10])}"
        };

        return string.Join(' ', new[] { head, tail }.Where(p => p.Length > 0));
    }
}
