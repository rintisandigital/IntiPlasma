using System.Globalization;

namespace Domain.Documents.Attachments;

/// <summary>
/// The entity whose <c>Documents</c> list refers to attachments.
/// </summary>
public sealed record AttachmentOwner(string Type, Guid Id, string Key)
{
    public const int TypeMaxLength = 50;
    public const int KeyMaxLength = 20;

    public static AttachmentOwner Of(string type, Guid id) => new(type, id, string.Empty);

    public static AttachmentOwner OfRevision(Guid dailyRecordingId, int revisionNumber) => new(
        AttachmentOwnerTypes.DailyRecordingRevision,
        dailyRecordingId,
        revisionNumber.ToString(CultureInfo.InvariantCulture));
}
