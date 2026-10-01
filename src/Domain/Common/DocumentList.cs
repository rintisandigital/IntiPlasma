using SharedKernel;

namespace Domain.Common;

/// <summary>
/// Rules for the list of attachment ids (<c>Documents</c>) carried by masters and transactions.
/// The attachments themselves live in the Documents context; entities only keep their ids.
/// </summary>
public static class DocumentList
{
    public const int MaxDocuments = 20;

    /// <summary>
    /// Removes duplicates (keeping the order) and rejects empty ids or too many documents.
    /// </summary>
    public static Result<Guid[]> Normalize(IEnumerable<Guid>? documents)
    {
        Guid[] normalized = documents?.Distinct().ToArray() ?? [];

        if (normalized.Contains(Guid.Empty))
        {
            return Result.Failure<Guid[]>(DocumentErrors.Invalid);
        }

        if (normalized.Length > MaxDocuments)
        {
            return Result.Failure<Guid[]>(DocumentErrors.TooMany);
        }

        return normalized;
    }

    /// <summary>
    /// Normalizes <paramref name="documents"/> and hands the result to <paramref name="apply"/> (the entity's setter).
    /// </summary>
    public static Result Apply(IEnumerable<Guid>? documents, Action<Guid[]> apply)
    {
        Result<Guid[]> normalized = Normalize(documents);
        if (normalized.IsFailure)
        {
            return Result.Failure(normalized.Error);
        }

        apply(normalized.Value);

        return Result.Success();
    }
}
