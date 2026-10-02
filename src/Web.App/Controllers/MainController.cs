using Microsoft.AspNetCore.Mvc;

namespace Web.App.Controllers;

public class MainController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ExampleList()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Dashboard()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ExampleForm()
    {
        return View();
    }
}
