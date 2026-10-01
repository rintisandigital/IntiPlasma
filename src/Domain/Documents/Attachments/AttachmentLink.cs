namespace Domain.Documents.Attachments;

/// <summary>
/// Reverse index of the <c>Documents</c> lists: which entity refers to which attachment. One attachment can have
/// several owners (a feed mutation keeps the same attachments on its stock return and its stock transfer).
/// </summary>
public sealed class AttachmentLink
{
    public AttachmentLink(Guid attachmentId, AttachmentOwner owner)
    {
        AttachmentId = attachmentId;
        OwnerType = owner.Type;
        OwnerId = owner.Id;
        OwnerKey = owner.Key;
    }

    private AttachmentLink()
    {
    }

    public Guid AttachmentId { get; private set; }

    /// <summary>
    /// One of <see cref="AttachmentOwnerTypes"/>.
    /// </summary>
    public string OwnerType { get; private set; }

    public Guid OwnerId { get; private set; }

    /// <summary>
    /// Identifies a child without its own id (the revision number of a daily recording); empty otherwise.
    /// </summary>
    public string OwnerKey { get; private set; }
}
