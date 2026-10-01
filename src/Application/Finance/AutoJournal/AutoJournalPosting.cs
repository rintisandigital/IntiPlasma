using SharedKernel;

namespace Application.Finance.AutoJournal;

internal static class AutoJournalPosting
{
    /// <summary>
    /// Throws on failure so the outbox retries the event and finally records the error.
    /// </summary>
    public static async Task EnsureAsync(Task<Result<Guid>> posting, string source)
    {
        Result<Guid> result = await posting;

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Auto journal for {source} failed: {result.Error.Code} - {result.Error.Description}");
        }
    }
}
