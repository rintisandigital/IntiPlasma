using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IntegrationTests;

[Collection(nameof(IntegrationTestCollection))]
public abstract class BaseIntegrationTest
{
    protected const string Password = "Password123";

    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        Services = factory.Services;
        HttpClient = factory.CreateClient();
        HttpClient.BaseAddress = new Uri(HttpClient.BaseAddress!, "api/v1/");
    }

    protected HttpClient HttpClient { get; }

    /// <summary>
    /// The API's services, for arranging data that would take many requests to build (stock, running cycles).
    /// </summary>
    protected IServiceProvider Services { get; }

    protected sealed record AccessTokens(string AccessToken, string RefreshToken);

    protected static string UniqueEmail() => $"test-{Guid.NewGuid():N}@example.com";

    /// <summary>
    /// Registers a user as the seeded administrator (registration requires the users:manage permission).
    /// </summary>
    protected async Task<Guid> RegisterUserAsync(string email)
    {
        AccessTokens adminTokens = await LoginAsync(
            IntegrationTestWebAppFactory.AdminEmail,
            IntegrationTestWebAppFactory.AdminPassword);

        using var request = new HttpRequestMessage(HttpMethod.Post, "users/register")
        {
            Content = JsonContent.Create(new
            {
                email,
                firstName = "Test",
                lastName = "User",
                password = Password
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        HttpResponseMessage response = await HttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    protected async Task<AccessTokens> LoginAsync(string email, string password = Password)
    {
        var request = new { email, password };

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/login", request);
        response.EnsureSuccessStatusCode();

        AccessTokens? tokens = await response.Content.ReadFromJsonAsync<AccessTokens>();

        return tokens!;
    }

    protected async Task<(Guid UserId, AccessTokens Tokens)> RegisterAndLoginAsync()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);

        return (userId, tokens);
    }

    protected async Task AuthenticateAsAdminAsync()
    {
        AccessTokens tokens = await LoginAsync(
            IntegrationTestWebAppFactory.AdminEmail,
            IntegrationTestWebAppFactory.AdminPassword);

        Authenticate(tokens.AccessToken);
    }

    protected void Authenticate(string accessToken)
    {
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
