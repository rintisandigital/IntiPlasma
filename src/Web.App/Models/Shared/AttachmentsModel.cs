using Application.Documents;
using Domain.Common;

namespace Web.App.Models.Shared;

/// <summary>
/// Model of <c>_Attachments</c>: the attachments of a form. Each attachment is posted as a hidden
/// <c>Documents</c> value; the use case links new ones and releases removed ones.
/// </summary>
public sealed record AttachmentsModel(IReadOnlyList<AttachmentResponse> Attachments, bool Editable, string FieldName = "Documents")
{
    public int MaxAttachments => DocumentList.MaxDocuments;
}

/// <summary>
/// Model of <c>_AttachmentsCard</c>: read-only when <see cref="SaveUrl"/> is null.
/// </summary>
public sealed record AttachmentsCardModel(IReadOnlyList<AttachmentResponse> Attachments, string? SaveUrl);
