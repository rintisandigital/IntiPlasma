namespace MobileApp.Core.Api;

/// <summary>
/// What went wrong with a call to Web.Api, ready to show to the user.
/// </summary>
public sealed record ApiError
{
    /// <summary>
    /// The request never got an answer (no connection, server unreachable, timeout).
    /// </summary>
    public const string NetworkCode = "Client.Network";

    /// <summary>
    /// The session ended (refresh token rejected); the user must sign in again.
    /// </summary>
    public const string SessionExpiredCode = "Client.SessionExpired";

    /// <summary>
    /// The server answered with something the client cannot read.
    /// </summary>
    public const string UnexpectedCode = "Client.Unexpected";

    /// <summary>
    /// HTTP status, 0 when there was no response.
    /// </summary>
    public int Status { get; init; }

    /// <summary>
    /// Error code from Web.Api (ProblemDetails <c>title</c>, e.g. <c>Users.NotFoundByEmail</c>) or a client code.
    /// </summary>
    public string Code { get; init; } = UnexpectedCode;

    /// <summary>
    /// Message for the user (Bahasa Indonesia, see <see cref="ErrorMessages"/>).
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Original server description (English), kept for logs and support.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>
    /// Validation errors (code → message) when the server rejected the input.
    /// </summary>
    public IReadOnlyList<ApiFieldError> FieldErrors { get; init; } = [];

    public bool IsNetwork => Code == NetworkCode;

    public bool IsSessionExpired => Code == SessionExpiredCode;
}

public sealed record ApiFieldError(string Code, string Message);

/// <summary>
/// Outcome of a call without a response body.
/// </summary>
public class ApiResult
{
    protected ApiResult(ApiError? error)
    {
        Error = error;
    }

    public ApiError? Error { get; }

    public bool IsSuccess => Error is null;

    public static ApiResult Success() => new(null);

    public static ApiResult Failure(ApiError error) => new(error);
}

/// <summary>
/// Outcome of a call with a response body.
/// </summary>
public sealed class ApiResult<T> : ApiResult
{
    private readonly T? _value;

    private ApiResult(T? value, ApiError? error)
        : base(error)
    {
        _value = value;
    }

    /// <summary>
    /// The response body; only valid when <see cref="ApiResult.IsSuccess"/>.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    public static ApiResult<T> Success(T value) => new(value, null);

    public static new ApiResult<T> Failure(ApiError error) => new(default, error);
}
