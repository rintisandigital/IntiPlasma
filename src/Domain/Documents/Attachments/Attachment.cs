using SharedKernel;

namespace Domain.Documents.Attachments;

/// <summary>
/// Lampiran: metadata of an uploaded photo or document. The file itself is kept by the file storage
/// under <see cref="StoragePath"/>; masters and transactions refer to it by id in their <c>Documents</c> list.
/// An attachment starts <see cref="AttachmentStatus.Temporary"/> and becomes <see cref="AttachmentStatus.Linked"/>
/// while at least one entity refers to it (tracked by <see cref="AttachmentLink"/>).
/// </summary>
public sealed class Attachment : AggregateRoot, ISoftDeletable
{
    public const int FileNameMaxLength = 255;
    public const int DescriptionMaxLength = 500;
    private const string DefaultFileName = "file";

    private Attachment(Guid id)
        : base(id)
    {
    }

    private Attachment()
    {
    }

    /// <summary>
    /// Original file name sent by the client, without any path. Never used as a storage path.
    /// </summary>
    public string FileName { get; private set; }

    /// <summary>
    /// File name in the storage: <c>{Id}{Extension}</c>.
    /// </summary>
    public string StoredFileName { get; private set; }

    public string Extension { get; private set; }

    /// <summary>
    /// MIME type detected from the file content (magic bytes).
    /// </summary>
    public string ContentType { get; private set; }

    public long SizeBytes { get; private set; }

    /// <summary>
    /// Path relative to the storage root, e.g. <c>2026/10/{Id}.jpg</c>.
    /// </summary>
    public string StoragePath { get; private set; }

    /// <summary>
    /// SHA-256 of the content (lower-case hex).
    /// </summary>
    public string Checksum { get; private set; }

    public AttachmentKind Kind { get; private set; }

    /// <summary>
    /// Branch of the first owner that has one; empty while temporary or when owned by a company-wide master (vendor, customer).
    /// </summary>
    public Guid? BranchId { get; private set; }

    public string? Description { get; private set; }

    public AttachmentStatus Status { get; private set; }

    /// <summary>
    /// When the attachment last lost its final link; together with <see cref="AggregateRoot.CreatedAtUtc"/>
    /// it decides when an unused attachment is purged.
    /// </summary>
    public DateTime? UnlinkedAtUtc { get; private set; }

#pragma warning disable S1144 // Private setters are used by EF Core and the audit interceptor (soft delete).
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid? DeletedBy { get; private set; }
#pragma warning restore S1144

    public static Result<Attachment> Create(
        Guid id,
        string fileName,
        AttachmentFile file,
        string checksum,
        DateOnly uploadDate,
        string? description)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Attachment>(AttachmentErrors.InvalidId);
        }

        if (file.SizeBytes <= 0)
        {
            return Result.Failure<Attachment>(AttachmentErrors.Empty);
        }

        string storedFileName = $"{id:N}{file.Extension}";

        return new Attachment(id)
        {
            FileName = SanitizeFileName(fileName),
            StoredFileName = storedFileName,
            Extension = file.Extension,
            ContentType = file.ContentType,
            SizeBytes = file.SizeBytes,
            StoragePath = $"{uploadDate:yyyy}/{uploadDate:MM}/{storedFileName}",
            Checksum = checksum,
            Kind = file.Kind,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Status = AttachmentStatus.Temporary
        };
    }

    /// <summary>
    /// Strips any directory part and control characters; keeps the extension when the name is shortened.
    /// </summary>
    public static string SanitizeFileName(string? fileName)
    {
        string name = (fileName ?? string.Empty).Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        name = new string([.. name.Where(c => !char.IsControl(c))]).Trim().Trim('.').Trim();

        if (name.Length == 0)
        {
            return DefaultFileName;
        }

        if (name.Length <= FileNameMaxLength)
        {
            return name;
        }

        int dot = name.LastIndexOf('.');
        string extension = dot > 0 && name.Length - dot <= 10 ? name[dot..] : string.Empty;

        return name[..(FileNameMaxLength - extension.Length)] + extension;
    }

    /// <summary>
    /// The attachment is now referred to by an owner. The branch of the first owner is adopted;
    /// an attachment of one branch cannot be attached to a document of another branch.
    /// </summary>
    public Result Link(Guid? ownerBranchId)
    {
        if (ownerBranchId is not null && BranchId is not null && BranchId != ownerBranchId)
        {
            return Result.Failure(AttachmentErrors.BranchMismatch(Id));
        }

        BranchId ??= ownerBranchId;
        Status = AttachmentStatus.Linked;
        UnlinkedAtUtc = null;

        return Result.Success();
    }

    /// <summary>
    /// The last owner released the attachment; it becomes temporary and is purged after the retention time.
    /// </summary>
    public void Unlink(DateTime utcNow)
    {
        Status = AttachmentStatus.Temporary;
        UnlinkedAtUtc = utcNow;
    }

    public Result EnsureDeletable() =>
        Status == AttachmentStatus.Linked ? Result.Failure(AttachmentErrors.InUse(Id)) : Result.Success();

    /// <summary>
    /// True when the same file (same content) is uploaded again with the same id, e.g. a retry of the mobile app.
    /// </summary>
    public bool IsSameContent(string checksum, long sizeBytes) =>
        SizeBytes == sizeBytes && string.Equals(Checksum, checksum, StringComparison.OrdinalIgnoreCase);
}
