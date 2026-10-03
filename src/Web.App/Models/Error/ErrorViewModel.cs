namespace Web.App.Models.Error;

public sealed record ErrorViewModel(int StatusCode, string Title, string Message, string Image, string RequestId)
{
    public static ErrorViewModel For(int statusCode, string requestId) => statusCode switch
    {
        StatusCodes.Status403Forbidden => new(statusCode, "Access denied",
            "You do not have access to this page. Contact your administrator if you need it.",
            "assets/img/authentication/error-404.png", requestId),
        StatusCodes.Status404NotFound => new(statusCode, "Page not found",
            "The page you are looking for does not exist or has been moved.",
            "assets/img/authentication/error-404.png", requestId),
        StatusCodes.Status429TooManyRequests => new(statusCode, "Too many attempts",
            "Too many sign-in attempts from this device. Wait a minute and try again.",
            "assets/img/authentication/error-404.png", requestId),
        _ when statusCode >= 500 => new(statusCode, "Something went wrong",
            "An unexpected error occurred. Please try again; if it keeps happening, report the request id below.",
            "assets/img/authentication/error-500.png", requestId),
        _ => new(statusCode, "Request failed",
            "The request could not be completed.",
            "assets/img/authentication/error-404.png", requestId)
    };
}
