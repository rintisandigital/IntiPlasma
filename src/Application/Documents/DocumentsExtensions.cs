using Domain.Common;
using Domain.Documents.Attachments;
using FluentValidation;
using SharedKernel;

namespace Application.Documents;

public static class DocumentsExtensions
{
    /// <summary>
    /// Applies the <c>documents</c> of a create/update command to <paramref name="entity"/> and links the attachments.
    /// <c>null</c> leaves the list unchanged (create: empty), an empty list removes every attachment.
    /// Call before saving: the links are saved together with the entity.
    /// </summary>
    public static Task<Result> ApplyDocumentsAsync(
        this IAttachmentService attachments,
        IHasDocuments entity,
        AttachmentOwner owner,
        Guid? branchId,
        IReadOnlyList<Guid>? documents,
        CancellationToken cancellationToken) =>
        attachments.ApplyDocumentsAsync(
            entity.SetDocuments, () => entity.Documents, owner, branchId, documents, cancellationToken);

    /// <summary>
    /// Same as above for a child entity whose list is changed through its aggregate (harvest, recording revision).
    /// </summary>
    public static async Task<Result> ApplyDocumentsAsync(
        this IAttachmentService attachments,
        Func<IEnumerable<Guid>?, Result> setDocuments,
        Func<Guid[]> currentDocuments,
        AttachmentOwner owner,
        Guid? branchId,
        IReadOnlyList<Guid>? documents,
        CancellationToken cancellationToken)
    {
        if (documents is null)
        {
            return Result.Success();
        }

        Result set = setDocuments(documents);

        return set.IsFailure
            ? set
            : await attachments.SyncLinksAsync(owner, branchId, currentDocuments(), cancellationToken);
    }

    public static IRuleBuilderOptions<T, IReadOnlyList<Guid>?> ValidDocuments<T>(this IRuleBuilder<T, IReadOnlyList<Guid>?> rule) =>
        rule
            .Must(documents => documents is null || documents.Distinct().Count() <= DocumentList.MaxDocuments)
            .WithMessage($"At most {DocumentList.MaxDocuments} documents can be attached")
            .Must(documents => documents is null || !documents.Contains(Guid.Empty))
            .WithMessage("A document id must not be empty");
}
