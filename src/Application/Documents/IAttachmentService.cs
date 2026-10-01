using Domain.Documents.Attachments;
using SharedKernel;

namespace Application.Documents;

/// <summary>
/// Lampiran: saves, reads and deletes attachments (metadata + file) and keeps track of the entities using them.
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// Validates the size and type, stores the file and its metadata. The attachment is temporary until an
    /// entity refers to it.
    /// </summary>
    Task<Result<AttachmentResponse>> SaveAsync(AttachmentUpload upload, CancellationToken cancellationToken = default);

    Task<Result<AttachmentResponse>> GetAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Metadata of several attachments (e.g. the <c>documents</c> of a document); ids that do not exist or
    /// are not visible to the user are left out.
    /// </summary>
    Task<IReadOnlyList<AttachmentResponse>> GetManyAsync(
        IReadOnlyCollection<Guid> attachmentIds,
        CancellationToken cancellationToken = default);

    Task<Result<AttachmentContent>> OpenAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an attachment that is no longer used by any entity (soft delete of the metadata, the file is removed).
    /// </summary>
    Task<Result> DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks, without changing anything, that <paramref name="documents"/> can be attached to a document of
    /// <paramref name="ownerBranchId"/>. Handlers that take a document number call it before taking the number,
    /// so an invalid attachment does not burn a number.
    /// </summary>
    Task<Result> EnsureAttachableAsync(
        Guid? ownerBranchId,
        IReadOnlyCollection<Guid>? documents,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes the links of <paramref name="owner"/> match its <paramref name="documents"/> list: new attachments are
    /// checked (exist, visible, same branch) and linked, removed ones are released. Does not save; the caller saves
    /// together with the owner.
    /// </summary>
    Task<Result> SyncLinksAsync(
        AttachmentOwner owner,
        Guid? ownerBranchId,
        IReadOnlyCollection<Guid> documents,
        CancellationToken cancellationToken = default);
}
