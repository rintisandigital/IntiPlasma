using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.App.Infrastructure.Results;

namespace Web.App.Controllers;

/// <summary>
/// Base controller: toast notifications (rendered by the layouts) and use case errors on forms.
/// </summary>
public abstract class AppController : Controller
{
    public const string SuccessKey = "success";
    public const string ErrorKey = "error";

    protected void NotifySuccess(string message) => TempData[SuccessKey] = message;

    protected void NotifyError(string message) => TempData[ErrorKey] = message;

    protected void AddErrors(Error error) => error.AddToModelState(ModelState);
}
