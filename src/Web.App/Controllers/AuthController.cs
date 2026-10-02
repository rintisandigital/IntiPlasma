using Microsoft.AspNetCore.Mvc;

namespace Web.App.Controllers;

public class AuthController : Controller
{
    [HttpGet]
    public IActionResult Index() => RedirectToAction(nameof(Login));

    [HttpGet]
    [HttpPost]
    public IActionResult Login()
    {
        // if post
        if(Request.Method == "POST")
        {
            // Handle login logic here
            // For example, validate user credentials and sign in the user
            return RedirectToAction("Index", "Main");
        }
        return View();
    }
}
