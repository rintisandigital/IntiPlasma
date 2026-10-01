namespace Domain.Documents.Attachments;

public enum AttachmentKind
{
    Photo = 1,
    Document = 2
}

public enum AttachmentStatus
{
    /// <summary>
    /// Uploaded but not (or no longer) referred to by any entity; purged after the retention time.
    /// </summary>
    Temporary = 1,

    /// <summary>
    /// Referred to by at least one entity; cannot be deleted.
    /// </summary>
    Linked = 2
}
