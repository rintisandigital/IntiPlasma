namespace Infrastructure.Documents;

internal sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Root folder of the uploaded files; relative paths are resolved against the application folder.
    /// </summary>
    public string RootPath { get; init; } = "uploads";

    /// <summary>
    /// Temporary attachments (uploaded but never linked, or released by their last owner) older than this are purged.
    /// </summary>
    public int OrphanRetentionHours { get; init; } = 24;

    public int CleanupIntervalMinutes { get; init; } = 60;

    public int CleanupBatchSize { get; init; } = 100;
}
