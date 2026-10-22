using Application.Abstractions.Messaging;
using Application.Documents;
using Application.Users.ChangePassword;
using Application.Users.GetSession;
using Application.Users.Profile;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Infrastructure.Auth;
using Web.App.Infrastructure.Authorization;
using Web.App.Models.Account;

namespace Web.App.Controllers;

/// <summary>
/// The signed-in user's own account (opened in the content iframe from the user menu): profile with photo, and
/// change password.
/// </summary>
[AuthenticatedOnly]
public sealed class AccountController(
    ICommandHandler<ChangeOwnPasswordCommand> changePassword,
    IQueryHandler<GetUserSessionQuery, UserSessionResponse> sessionQuery) : AppController
{
    // The photo is limited to 10 MB; the rest is room for the multipart envelope.
    private const long MaxPhotoRequestBytes = AttachmentFileTypes.MaxSizeBytes + 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> Profile(
        [FromServices] IQueryHandler<GetMyProfileQuery, MyProfileResponse> profileQuery,
        CancellationToken cancellationToken)
    {
        Result<MyProfileResponse> profile = await profileQuery.Handle(new GetMyProfileQuery(), cancellationToken);

        return profile.IsSuccess ? View(profile.Value) : NotFound();
    }

    /// <summary>
    /// Uploads a new profile photo (replacing the current one).
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxPhotoRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxPhotoRequestBytes)]
    public async Task<IActionResult> Photo(
        IFormFile? photo,
        [FromServices] IAttachmentService attachments,
        [FromServices] ICommandHandler<SetMyPhotoCommand> setPhoto,
        CancellationToken cancellationToken)
    {
        if (photo is null || photo.Length == 0)
        {
            NotifyError("Choose a photo to upload.");
            return RedirectToAction(nameof(Profile));
        }

        await using Stream content = photo.OpenReadStream();
        Result<AttachmentResponse> uploaded = await attachments.SaveAsync(
            new AttachmentUpload(content, photo.FileName, null, "Profile photo"), cancellationToken);

        Result result = uploaded.IsSuccess
            ? await setPhoto.Handle(new SetMyPhotoCommand(uploaded.Value.Id), cancellationToken)
            : uploaded;

        if (result.IsFailure)
        {
            NotifyError(result.Error.Description);
        }
        else
        {
            NotifySuccess("Your profile photo has been updated.");
        }

        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    public async Task<IActionResult> RemovePhoto(
        [FromServices] ICommandHandler<SetMyPhotoCommand> setPhoto,
        CancellationToken cancellationToken)
    {
        Result result = await setPhoto.Handle(new SetMyPhotoCommand(null), cancellationToken);

        if (result.IsFailure)
        {
            NotifyError(result.Error.Description);
        }
        else
        {
            NotifySuccess("Your profile photo has been removed.");
        }

        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        Result result = await changePassword.Handle(
            new ChangeOwnPasswordCommand(model.CurrentPassword, model.NewPassword),
            cancellationToken);

        if (result.IsFailure)
        {
            AddErrors(result.Error);
            return View(model);
        }

        // The password change renewed the security stamp: re-issue this session's cookie so only the other
        // sessions are signed out.
        Guid userId = User.GetUserId()!.Value;
        Result<UserSessionResponse> session = await sessionQuery.Handle(new GetUserSessionQuery(userId), cancellationToken);

        if (session.IsSuccess)
        {
            AuthenticateResult current = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                User.WithSecurityStamp(session.Value.SecurityStamp),
                current.Properties ?? new AuthenticationProperties());
        }

        NotifySuccess("Your password has been changed.");

        return RedirectToAction(nameof(ChangePassword));
    }
}
