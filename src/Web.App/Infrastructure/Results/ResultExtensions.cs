using Microsoft.AspNetCore.Mvc.ModelBinding;
using SharedKernel;

namespace Web.App.Infrastructure.Results;

public static class ResultExtensions
{
    /// <summary>
    /// Adds a failed use case's error to the form. Command validation errors carry no property name, so they
    /// are listed in the validation summary; field-level checks live on the view models (data annotations).
    /// </summary>
    public static void AddToModelState(this Error error, ModelStateDictionary modelState)
    {
        foreach (string message in error.Messages())
        {
            modelState.AddModelError(string.Empty, message);
        }
    }

    public static IReadOnlyList<string> Messages(this Error error) =>
        error is ValidationError validation
            ? [.. validation.Errors.Select(e => e.Description)]
            : [error.Description];
}
