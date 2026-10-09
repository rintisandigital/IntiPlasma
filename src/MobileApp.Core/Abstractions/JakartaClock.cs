namespace MobileApp.Core.Abstractions;

/// <summary>
/// The device clock with dates in Western Indonesia Time (UTC+7, no daylight saving), whatever time zone the
/// device is set to.
/// </summary>
public sealed class JakartaClock : IClock
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(UtcNow + Offset);
}
