using System.Net;
using System.Net.Http.Json;
using MobileApp.Core.Abstractions;
using MobileApp.Core.Api;
using MobileApp.Core.Session;

namespace MobileApp.UnitTests;

/// <summary>
/// HTTP server stand-in: answers each request with the given function (after an optional delay) and records what
/// was sent. Throwing from the function simulates an unreachable server.
/// </summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> respond, TimeSpan delay = default)
    : HttpMessageHandler
{
    private readonly Lock _gate = new();

    public List<(HttpMethod Method, string Path, string? Authorization, string? Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        lock (_gate)
        {
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization?.Parameter, body));
        }

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }

        return respond(request, body);
    }

    public static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = JsonContent.Create(value, value.GetType(), options: ApiClient.JsonOptions) };

    public static HttpResponseMessage Problem(HttpStatusCode status, string title, string detail, object[]? errors = null) =>
        new(status)
        {
            Content = JsonContent.Create(
                new { type = "about:blank", title, status = (int)status, detail, errors },
                options: ApiClient.JsonOptions)
        };

    public static HttpResponseMessage Offline(HttpRequestMessage request, string? body) =>
        throw new HttpRequestException("No such host is known.");
}

/// <summary>
/// The app's HTTP stack against fake servers, wired like <c>MauiProgram</c>: <see cref="ApiClient"/> with
/// <see cref="AuthHandler"/> for API calls, a separate plain client for the token refresh.
/// </summary>
internal sealed class FakeApi : IDisposable
{
    private readonly HttpClient _refreshClient;
    private readonly AuthHandler _authHandler;
    private readonly HttpClient _apiClient;

    public FakeApi(
        Func<HttpRequestMessage, string?, HttpResponseMessage> api,
        Func<HttpRequestMessage, string?, HttpResponseMessage>? refresh = null,
        ISecureStore? secureStore = null,
        IAppSettings? settings = null,
        TimeSpan refreshDelay = default)
    {
        SecureStore = secureStore ?? new InMemorySecureStore();
        Settings = settings ?? new TestSettings();
        Tokens = new TokenStore(SecureStore);

        Api = new StubHttpHandler(api);
        Auth = new StubHttpHandler(refresh ?? api, refreshDelay);

        _refreshClient = new HttpClient(Auth, disposeHandler: false);
        Refresher = new TokenRefresher(_refreshClient, Settings, Tokens);

        _authHandler = new AuthHandler(Tokens, Refresher) { InnerHandler = Api };
        _apiClient = new HttpClient(_authHandler, disposeHandler: false);
        Client = new ApiClient(_apiClient, Settings);
    }

    public ISecureStore SecureStore { get; }

    public IAppSettings Settings { get; }

    public TokenStore Tokens { get; }

    public TokenRefresher Refresher { get; }

    public StubHttpHandler Api { get; }

    public StubHttpHandler Auth { get; }

    public ApiClient Client { get; }

    public void Dispose()
    {
        _apiClient.Dispose();
        _authHandler.Dispose();
        _refreshClient.Dispose();
        Refresher.Dispose();
        Api.Dispose();
        Auth.Dispose();
    }
}

internal sealed class InMemorySecureStore : ISecureStore
{
    public Dictionary<string, string> Values { get; } = [];

    public Task<string?> GetAsync(string key) => Task.FromResult(Values.GetValueOrDefault(key));

    public Task SetAsync(string key, string value)
    {
        Values[key] = value;

        return Task.CompletedTask;
    }

    public void Remove(string key) => Values.Remove(key);
}

internal sealed class TestSettings : IAppSettings
{
    public string ServerUrl { get; set; } = "http://api.test/";

    public string? LastEmail { get; set; }

    public bool CanEditServerUrl => true;
}
