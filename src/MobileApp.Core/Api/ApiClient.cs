using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MobileApp.Core.Abstractions;
using MobileApp.Core.Contracts;

namespace MobileApp.Core.Api;

/// <summary>
/// Thin JSON client for Web.Api (<c>{ServerUrl}/api/v1/…</c>). Never throws for HTTP or network failures: every
/// call returns an <see cref="ApiResult"/> whose error is already translated for the user. Bearer tokens and the
/// one-time refresh after a 401 are handled by <see cref="AuthHandler"/> in the HttpClient pipeline.
/// </summary>
public sealed class ApiClient(HttpClient httpClient, IAppSettings settings)
{
    public const string ApiPrefix = "api/v1/";

    public const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>
    /// Same conventions as Web.Api: camelCase properties, enums as strings.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(settings.ServerUrl, path));

        return await SendAsync<T>(request, cancellationToken);
    }

    /// <summary>
    /// A binary response (attachment file) with its content type.
    /// </summary>
    public async Task<ApiResult<AttachmentContent>> GetBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(settings.ServerUrl, path));

        (HttpResponseMessage? response, ApiError? networkError) = await TrySendAsync(request, cancellationToken);
        if (response is null)
        {
            return ApiResult<AttachmentContent>.Failure(networkError!);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<AttachmentContent>.Failure(await ReadErrorAsync(response, cancellationToken));
            }

            byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

            return ApiResult<AttachmentContent>.Success(new AttachmentContent(contentType, bytes));
        }
    }

    public async Task<ApiResult<T>> PostAsync<T>(string path, object? body, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateWithBody(HttpMethod.Post, path, body);

        return await SendAsync<T>(request, cancellationToken);
    }

    /// <summary>
    /// A create with the <c>Idempotency-Key</c> header (PLAN-MOBILE §3.6): the offline queue uses the document id, so
    /// a resend after a lost response is answered with the first result.
    /// </summary>
    public async Task<ApiResult<T>> PostAsync<T>(
        string path,
        object? body,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateWithBody(HttpMethod.Post, path, body);
        request.Headers.Add(IdempotencyKeyHeader, idempotencyKey.ToString());

        return await SendAsync<T>(request, cancellationToken);
    }

    /// <summary>
    /// A multipart upload of one file (form field <c>file</c>) with an optional client id (form field <c>id</c>),
    /// as <c>POST attachments</c> expects.
    /// </summary>
    public async Task<ApiResult<T>> PostFileAsync<T>(
        string path,
        byte[] content,
        string fileName,
        string contentType,
        Guid? id,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);

        using var idField = new StringContent(id?.ToString() ?? string.Empty);
        if (id is not null)
        {
            form.Add(idField, "id");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(settings.ServerUrl, path)) { Content = form };

        return await SendAsync<T>(request, cancellationToken);
    }

    public async Task<ApiResult> PostAsync(string path, object? body, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateWithBody(HttpMethod.Post, path, body);

        return await SendAsync(request, cancellationToken);
    }

    public async Task<ApiResult> PutAsync(string path, object? body, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateWithBody(HttpMethod.Put, path, body);

        return await SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Combines the configured server address with the API prefix and a relative path.
    /// </summary>
    public static Uri BuildUri(string serverAddress, string path)
    {
        string root = serverAddress.Trim();
        if (!root.EndsWith('/'))
        {
            root += "/";
        }

        return new Uri(new Uri(root, UriKind.Absolute), ApiPrefix + path.TrimStart('/'));
    }

    private HttpRequestMessage CreateWithBody(HttpMethod method, string path, object? body) =>
        new(method, BuildUri(settings.ServerUrl, path))
        {
            Content = body is null ? null : JsonContent.Create(body, body.GetType(), options: JsonOptions)
        };

    private async Task<ApiResult<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        (HttpResponseMessage? response, ApiError? networkError) = await TrySendAsync(request, cancellationToken);
        if (response is null)
        {
            return ApiResult<T>.Failure(networkError!);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<T>.Failure(await ReadErrorAsync(response, cancellationToken));
            }

            try
            {
                T? value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);

                return value is null
                    ? ApiResult<T>.Failure(Unexpected((int)response.StatusCode, "Empty response body."))
                    : ApiResult<T>.Success(value);
            }
            catch (JsonException exception)
            {
                return ApiResult<T>.Failure(Unexpected((int)response.StatusCode, exception.Message));
            }
        }
    }

    private async Task<ApiResult> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        (HttpResponseMessage? response, ApiError? networkError) = await TrySendAsync(request, cancellationToken);
        if (response is null)
        {
            return ApiResult.Failure(networkError!);
        }

        using (response)
        {
            return response.IsSuccessStatusCode
                ? ApiResult.Success()
                : ApiResult.Failure(await ReadErrorAsync(response, cancellationToken));
        }
    }

    private async Task<(HttpResponseMessage? Response, ApiError? Error)> TrySendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await httpClient.SendAsync(request, cancellationToken), null);
        }
        catch (HttpRequestException exception)
        {
            return (null, Network(exception.Message));
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation.
            return (null, Network(exception.Message));
        }
        catch (UriFormatException exception)
        {
            return (null, Network(exception.Message));
        }
    }

    /// <summary>
    /// Reads a ProblemDetails answer: the error code is in <c>title</c>, the description in <c>detail</c>, and
    /// validation errors in the <c>errors</c> extension (array of <c>{ code, description }</c>).
    /// </summary>
    internal static async Task<ApiError> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        int status = (int)response.StatusCode;

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new ApiError
            {
                Status = status,
                Code = ApiError.SessionExpiredCode,
                Message = ErrorMessages.For(ApiError.SessionExpiredCode, status, null)
            };
        }

        ProblemDetailsBody? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetailsBody>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // Not a ProblemDetails body (e.g. a proxy error page): fall back to the status code.
        }

        string code = string.IsNullOrWhiteSpace(problem?.Title) ? $"Http.{status}" : problem.Title;

        return new ApiError
        {
            Status = status,
            Code = code,
            Message = ErrorMessages.For(code, status, problem?.Detail),
            Detail = problem?.Detail,
            FieldErrors = problem?.Errors?
                .Select(e => new ApiFieldError(e.Code ?? string.Empty, e.Description ?? string.Empty))
                .ToList() ?? []
        };
    }

    private static ApiError Network(string detail) => new()
    {
        Code = ApiError.NetworkCode,
        Message = ErrorMessages.For(ApiError.NetworkCode, 0, null),
        Detail = detail
    };

    private static ApiError Unexpected(int status, string detail) => new()
    {
        Status = status,
        Code = ApiError.UnexpectedCode,
        Message = ErrorMessages.For(ApiError.UnexpectedCode, status, null),
        Detail = detail
    };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }

    internal sealed class ProblemDetailsBody
    {
        public string? Title { get; set; }

        public string? Detail { get; set; }

        public List<ProblemError>? Errors { get; set; }
    }

    internal sealed class ProblemError
    {
        public string? Code { get; set; }

        public string? Description { get; set; }
    }
}
