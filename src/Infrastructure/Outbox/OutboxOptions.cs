namespace Infrastructure.Outbox;

internal sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int IntervalInSeconds { get; init; } = 5;

    public int BatchSize { get; init; } = 50;

    /// <summary>
    /// After this many failed attempts a message is marked processed with its last error (dead letter).
    /// </summary>
    public int MaxAttempts { get; init; } = 5;
}
