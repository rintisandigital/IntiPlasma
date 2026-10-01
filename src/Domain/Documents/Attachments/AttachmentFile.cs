namespace Domain.Documents.Attachments;

/// <summary>
/// File properties detected from the uploaded content.
/// </summary>
public sealed record AttachmentFile(string ContentType, string Extension, AttachmentKind Kind, long SizeBytes);
