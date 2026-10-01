using Application.Documents;
using Domain.Documents.Attachments;
using Domain.Roles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Documents;

/// <summary>
/// Lampiran: upload a file first, then send its id in the <c>documents</c> of a master or transaction
/// (or later through <c>PUT …/{id}/documents</c>).
/// </summary>
internal sealed class AttachmentEndpoints : IEndpoint
{
    // The file itself is limited to 10 MB; the rest is room for the multipart envelope and form fields.
    private const long MaxRequestBytes = AttachmentFileTypes.MaxSizeBytes + 1024 * 1024;

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("attachments").WithTags(Tags.Attachments);

        group.MapPost("", async (
            IFormFile file,
            [FromForm] Guid? id,
            [FromForm] string? description,
            IAttachmentService attachments,
            CancellationToken cancellationToken) =>
        {
            if (description?.Length > Attachment.DescriptionMaxLength)
            {
                return CustomResults.Problem(Result.Failure(Error.Problem(
                    "Attachments.DescriptionTooLong", $"The description must not exceed {Attachment.DescriptionMaxLength} characters")));
            }

            await using Stream content = file.OpenReadStream();

            Result<AttachmentResponse> result = await attachments.SaveAsync(
                new AttachmentUpload(content, file.FileName, id, description), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.AttachmentsUpload)
        .DisableAntiforgery()
        .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))
        .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxRequestBytes });

        group.MapGet("", async (
            string? ids,
            IAttachmentService attachments,
            CancellationToken cancellationToken) =>
        {
            List<Guid> attachmentIds = [];
            foreach (string value in (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Guid.TryParse(value, out Guid attachmentId))
                {
                    return CustomResults.Problem(Result.Failure(Error.Problem("Attachments.InvalidIds", $"'{value}' is not a valid id")));
                }

                attachmentIds.Add(attachmentId);
            }

            if (attachmentIds.Count > 100)
            {
                return CustomResults.Problem(Result.Failure(Error.Problem("Attachments.TooManyIds", "At most 100 ids can be requested at once")));
            }

            IReadOnlyList<AttachmentResponse> result = await attachments.GetManyAsync(attachmentIds, cancellationToken);

            return Results.Ok(result);
        })
        .HasPermission(Permissions.AttachmentsRead);

        group.MapGet("{attachmentId:guid}", async (
            Guid attachmentId,
            IAttachmentService attachments,
            CancellationToken cancellationToken) =>
        {
            Result<AttachmentResponse> result = await attachments.GetAsync(attachmentId, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.AttachmentsRead);

        // Photos are shown inline unless ?download=true; documents are always downloaded.
        group.MapGet("{attachmentId:guid}/content", async (
            Guid attachmentId,
            bool? download,
            HttpContext httpContext,
            IAttachmentService attachments,
            CancellationToken cancellationToken) =>
        {
            Result<AttachmentContent> result = await attachments.OpenAsync(attachmentId, cancellationToken);
            if (result.IsFailure)
            {
                return CustomResults.Problem(result);
            }

            AttachmentContent file = result.Value;

            if (file.IsPhoto && download != true)
            {
                var disposition = new ContentDispositionHeaderValue("inline");
                disposition.SetHttpFileName(file.FileName);
                httpContext.Response.Headers.ContentDisposition = disposition.ToString();

                return Results.Stream(file.Content, file.ContentType, enableRangeProcessing: true);
            }

            return Results.Stream(file.Content, file.ContentType, file.FileName, enableRangeProcessing: true);
        })
        .HasPermission(Permissions.AttachmentsRead);

        group.MapDelete("{attachmentId:guid}", async (
            Guid attachmentId,
            IAttachmentService attachments,
            CancellationToken cancellationToken) =>
        {
            Result result = await attachments.DeleteAsync(attachmentId, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.AttachmentsDelete);
    }
}
