using Application.Abstractions.Messaging;
using Application.Users.SignIn;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SharedKernel;
using Web.App.Infrastructure.Auth;
using Web.App.Models.Auth;

namespace Web.App.Controllers;

[AllowAnonymous]
public sealed class AuthController(
    ICommandHandler<SignInUserCommand, SignedInUserResponse> signIn,
    IBranchContext branchContext) : AppController
{
    [HttpGet]
    public IActionResult Index() => RedirectToAction(nameof(Login));

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        var model = new LoginViewModel { ReturnUrl = returnUrl };
        #if DEBUG
        model.Email = "admin@intiplasma.local";
        model.Password = "Admin123!";
        #endif

        return View(model);
    }

    [HttpPost]
    [EnableRateLimiting(DependencyInjection.LoginRateLimitPolicy)]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        Result<SignedInUserResponse> result = await signIn.Handle(
            new SignInUserCommand(model.Email.Trim(), model.Password),
            cancellationToken);

        if (result.IsFailure)
        {
            AddErrors(result.Error);
            return View(model);
        }

        SignedInUserResponse user = result.Value;

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            UserPrincipal.Create(user.Id, user.Email, user.FirstName, user.LastName, user.SecurityStamp),
            new AuthenticationProperties { IsPersistent = model.RememberMe == true });

        // The branch selection of a previous user on this browser does not carry over.
        branchContext.Clear();

        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        branchContext.Clear();

        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction(nameof(MainController.Index), "Main");
}
