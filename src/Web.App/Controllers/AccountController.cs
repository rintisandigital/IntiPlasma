using Application.Abstractions.Messaging;
using Application.Users.ChangePassword;
using Application.Users.GetSession;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Infrastructure.Auth;
using Web.App.Models.Account;

namespace Web.App.Controllers;

/// <summary>
/// The signed-in user's own account (opened in the content iframe from the user menu).
/// </summary>
public sealed class AccountController(
    ICommandHandler<ChangeOwnPasswordCommand> changePassword,
    IQueryHandler<GetUserSessionQuery, UserSessionResponse> sessionQuery) : AppController
{
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
