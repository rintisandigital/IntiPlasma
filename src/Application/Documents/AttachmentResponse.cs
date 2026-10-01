namespace Application.Documents;

public sealed record AttachmentResponse
{
    public Guid Id { get; init; }

    public string FileName { get; init; }

    public string ContentType { get; init; }

    public string Extension { get; init; }

    public long SizeBytes { get; init; }

    public string Kind { get; init; }

    public string Checksum { get; init; }

    public string? Description { get; init; }

    public string Status { get; init; }

    public Guid? BranchId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public Guid? CreatedBy { get; init; }
}

/// <param name="Content">The file content; the caller disposes it.</param>
public sealed record AttachmentContent(Stream Content, string FileName, string ContentType, bool IsPhoto);

/// <param name="Id">Optional client-generated id; uploading the same content again with the same id is idempotent.</param>
public sealed record AttachmentUpload(Stream Content, string FileName, Guid? Id, string? Description);
