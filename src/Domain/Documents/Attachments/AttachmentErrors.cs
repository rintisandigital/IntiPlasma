using System.Globalization;
using SharedKernel;

namespace Domain.Documents.Attachments;

public static class AttachmentErrors
{
    public static Error NotFound(Guid attachmentId) => Error.NotFound(
        "Attachments.NotFound",
        $"The attachment with the Id = '{attachmentId}' was not found");

    /// <summary>
    /// A document refers to an attachment that does not exist (anymore) or is not visible to the user.
    /// </summary>
    public static Error Unknown(Guid attachmentId) => Error.Problem(
        "Attachments.Unknown",
        $"The attachment with the Id = '{attachmentId}' does not exist");

    public static Error InUse(Guid attachmentId) => Error.Conflict(
        "Attachments.InUse",
        $"The attachment with the Id = '{attachmentId}' is still attached to a document; remove it from the document first");

    public static Error BranchMismatch(Guid attachmentId) => Error.Problem(
        "Attachments.BranchMismatch",
        $"The attachment with the Id = '{attachmentId}' belongs to another branch");

    public static Error IdConflict(Guid attachmentId) => Error.Conflict(
        "Attachments.IdConflict",
        $"An attachment with the Id = '{attachmentId}' already exists with different content");

    public static readonly Error InvalidId = Error.Problem(
        "Attachments.InvalidId",
        "The attachment id must not be empty");

    public static readonly Error Empty = Error.Problem(
        "Attachments.Empty",
        "The file is empty");

    public static Error TooLarge(long maxBytes) => Error.Problem(
        "Attachments.TooLarge",
        string.Create(CultureInfo.InvariantCulture, $"The file must not be larger than {maxBytes / (1024 * 1024)} MB"));

    public static readonly Error UnsupportedType = Error.Problem(
        "Attachments.UnsupportedType",
        "Only JPEG, PNG, WEBP images and PDF documents can be uploaded");
}
