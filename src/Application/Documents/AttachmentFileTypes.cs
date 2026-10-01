using Domain.Documents.Attachments;

namespace Application.Documents;

/// <summary>
/// Accepted upload types, recognized from the first bytes of the content (magic bytes) rather than
/// the file name or the Content-Type header sent by the client.
/// </summary>
public static class AttachmentFileTypes
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Number of leading bytes needed to recognize every accepted type.
    /// </summary>
    public const int HeaderLength = 12;

    public static AttachmentFile? Detect(ReadOnlySpan<byte> header, long sizeBytes)
    {
        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return new AttachmentFile("image/jpeg", ".jpg", AttachmentKind.Photo, sizeBytes);
        }

        if (header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return new AttachmentFile("image/png", ".png", AttachmentKind.Photo, sizeBytes);
        }

        if (header.Length >= HeaderLength && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return new AttachmentFile("image/webp", ".webp", AttachmentKind.Photo, sizeBytes);
        }

        if (header.StartsWith("%PDF-"u8))
        {
            return new AttachmentFile("application/pdf", ".pdf", AttachmentKind.Document, sizeBytes);
        }

        return null;
    }
}
