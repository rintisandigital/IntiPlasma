namespace Domain.Common;

/// <summary>
/// Stock quantities are plain decimals stored with <see cref="Decimals"/> places (a <c>Quantity</c> value object was
/// deliberately not introduced: birds are integers, weights have 3 places and stock quantities 4, each with its own
/// UoM per line). Round derived quantities with <see cref="Round"/> so the value checked in memory equals the one
/// PostgreSQL stores.
/// </summary>
public static class Quantities
{
    public const int Decimals = 4;

    public static decimal Round(decimal quantity) =>
        decimal.Round(quantity, Decimals, MidpointRounding.AwayFromZero);
}
