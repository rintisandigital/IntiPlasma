using Application.Documents;
using Domain.Documents.Attachments;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Infrastructure.Authorization;

namespace Web.App.Controllers;

/// <summary>
/// Lampiran for the forms (PLAN-WEBAPP §7 W2): upload first, the returned id is posted with the form in
/// <c>Documents</c> and linked by the use case. Visibility follows the attachment service: a temporary upload is
/// only visible to its uploader, a linked one to users of the owner's branch. Saving the owning form still needs
/// the owner menu's Create/Edit right.
/// </summary>
[AuthenticatedOnly]
public sealed class AttachmentsController(IAttachmentService attachments) : AppController
{
    // The file is limited to 10 MB; the rest is room for the multipart envelope.
    private const long MaxRequestBytes = AttachmentFileTypes.MaxSizeBytes + 1024 * 1024;

    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Upload(IFormFile? file, string? description, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Choose a file to upload." });
        }

        if (description?.Length > Attachment.DescriptionMaxLength)
        {
            return BadRequest(new { message = $"The description must not exceed {Attachment.DescriptionMaxLength} characters." });
        }

        await using Stream content = file.OpenReadStream();

        Result<AttachmentResponse> result = await attachments.SaveAsync(
            new AttachmentUpload(content, file.FileName, null, description), cancellationToken);

        return result.IsSuccess
            ? Json(ToItem(result.Value))
            : BadRequest(new { message = result.Error.Description });
    }

    /// <summary>
    /// Opens the file: inline (photo/PDF preview in a new tab) or as a download.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> File(Guid id, bool download, CancellationToken cancellationToken)
    {
        Result<AttachmentContent> result = await attachments.OpenAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        AttachmentContent file = result.Value;

        return download
            ? File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: true)
            : File(file.Content, file.ContentType, enableRangeProcessing: true);
    }

    private object ToItem(AttachmentResponse attachment) => new
    {
        id = attachment.Id,
        fileName = attachment.FileName,
        sizeBytes = attachment.SizeBytes,
        kind = attachment.Kind,
        url = Url.Action(nameof(File), new { id = attachment.Id })
    };
}
