using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.App.Models.Error;

namespace Web.App.Controllers;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[Route("Error")]
public sealed class ErrorController : Controller
{
    /// <summary>
    /// Unhandled exceptions (production exception handler).
    /// </summary>
    [Route("")]
    public IActionResult Index() => Render(StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Error status codes (status code pages), e.g. /Error/404, and the cookie access-denied path /Error/403.
    /// </summary>
    [Route("{statusCode:int}")]
    public IActionResult Status(int statusCode) => Render(statusCode);

    private ViewResult Render(int statusCode)
    {
        Response.StatusCode = statusCode;

        return View("Error", ErrorViewModel.For(statusCode, Activity.Current?.Id ?? HttpContext.TraceIdentifier));
    }
}
