namespace SharedKernel;

/// <summary>
/// Monetary amount in the company's functional currency (IDR).
/// Values are rounded to two decimal places.
/// </summary>
public sealed record Money : IComparable<Money>
{
    public const int Decimals = 2;

    public static readonly Money Zero = new(0m);

    public Money(decimal amount)
    {
        Amount = decimal.Round(amount, Decimals, MidpointRounding.AwayFromZero);
    }

    public decimal Amount { get; private init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsZero => Amount == 0m;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsNegative => Amount < 0m;

    public static Money operator +(Money left, Money right) => Add(left, right);

    public static Money operator -(Money left, Money right) => Subtract(left, right);

    public static Money operator *(Money money, decimal factor) => Multiply(money, factor);

    public static Money operator -(Money money) => Negate(money);

    public static bool operator <(Money left, Money right) => Compare(left, right) < 0;

    public static bool operator >(Money left, Money right) => Compare(left, right) > 0;

    public static bool operator <=(Money left, Money right) => Compare(left, right) <= 0;

    public static bool operator >=(Money left, Money right) => Compare(left, right) >= 0;

    public static Money Add(Money left, Money right) => new(left.Amount + right.Amount);

    public static Money Subtract(Money left, Money right) => new(left.Amount - right.Amount);

    public static Money Multiply(Money money, decimal factor) => new(money.Amount * factor);

    public static Money Negate(Money money) => new(-money.Amount);

    public int CompareTo(Money? other) => other is null ? 1 : Amount.CompareTo(other.Amount);

    public override string ToString() => Amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

    private static int Compare(Money left, Money right) => left.CompareTo(right);
}
