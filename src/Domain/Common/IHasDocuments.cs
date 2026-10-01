using SharedKernel;

namespace Domain.Common;

/// <summary>
/// An entity that carries attachments (foto/dokumen) by their attachment ids.
/// </summary>
public interface IHasDocuments
{
    Guid[] Documents { get; }

    /// <summary>
    /// Replaces the whole list of attachments.
    /// </summary>
    Result SetDocuments(IEnumerable<Guid>? documents);
}
