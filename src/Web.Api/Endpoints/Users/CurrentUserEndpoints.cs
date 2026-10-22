using Application.Abstractions.Messaging;
using Application.Documents;
using Application.Users.ChangePassword;
using Application.Users.GetCurrent;
using Application.Users.Logout;
using Application.Users.Profile;
using Domain.Users;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// The signed-in user's own session (PLAN-MOBILE §4.1): profile with roles and permissions, logout of one device,
/// changing their own password and their profile photo. Any authenticated user may call these; no permission is
/// required (the photo endpoints do not need the attachment permissions either).
/// </summary>
internal sealed class CurrentUserEndpoints : IEndpoint
{
    // The photo is limited to 10 MB; the rest is room for the multipart envelope.
    private const long MaxPhotoRequestBytes = AttachmentFileTypes.MaxSizeBytes + 1024 * 1024;

    public sealed record LogoutRequest(string RefreshToken);

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/me", async (
            IQueryHandler<GetCurrentUserQuery, CurrentUserResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CurrentUserResponse> result = await handler.Handle(new GetCurrentUserQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .RequireAuthorization();

        app.MapPost("users/logout", async (
            LogoutRequest request,
            ICommandHandler<LogoutUserCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new LogoutUserCommand(request.RefreshToken), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .RequireAuthorization();

        app.MapPost("users/me/change-password", async (
            ChangePasswordRequest request,
            ICommandHandler<ChangeOwnPasswordCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new ChangeOwnPasswordCommand(request.CurrentPassword, request.NewPassword),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingPolicies.Authentication);

        // The current photo itself (404 when there is none); the id is also in users/me (photoId).
        app.MapGet("users/me/photo", async (
            IQueryHandler<GetMyProfileQuery, MyProfileResponse> profileQuery,
            IAttachmentService attachments,
            CancellationToken cancellationToken) =>
        {
            Result<MyProfileResponse> profile = await profileQuery.Handle(new GetMyProfileQuery(), cancellationToken);
            if (profile.IsFailure)
            {
                return CustomResults.Problem(profile);
            }

            if (profile.Value.PhotoId is not { } photoId)
            {
                return CustomResults.Problem(Result.Failure(UserErrors.PhotoNotFound));
            }

            Result<AttachmentContent> file = await attachments.OpenAsync(photoId, cancellationToken);

            return file.IsSuccess
                ? Results.Stream(file.Value.Content, file.Value.ContentType)
                : CustomResults.Problem(file);
        })
        .WithTags(Tags.Users)
        .RequireAuthorization();

        // Upload (form field "file", JPEG/PNG/WEBP) and set as the profile photo, replacing the previous one.
        app.MapPost("users/me/photo", async (
            IFormFile file,
            IAttachmentService attachments,
            ICommandHandler<SetMyPhotoCommand> handler,
            CancellationToken cancellationToken) =>
        {
            await using Stream content = file.OpenReadStream();

            Result<AttachmentResponse> uploaded = await attachments.SaveAsync(
                new AttachmentUpload(content, file.FileName, null, "Profile photo"), cancellationToken);
            if (uploaded.IsFailure)
            {
                return CustomResults.Problem(uploaded);
            }

            Result result = await handler.Handle(new SetMyPhotoCommand(uploaded.Value.Id), cancellationToken);

            return result.Match(() => Results.Ok(new { photoId = uploaded.Value.Id }), CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .RequireAuthorization()
        .DisableAntiforgery()
        .WithMetadata(new RequestSizeLimitAttribute(MaxPhotoRequestBytes))
        .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxPhotoRequestBytes });

        app.MapDelete("users/me/photo", async (
            ICommandHandler<SetMyPhotoCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new SetMyPhotoCommand(null), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .RequireAuthorization();
    }
}
