using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Roles;

public sealed class RolesTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly string[] UnknownPermissions = ["does-not:exist"];
    private static readonly string[] UsersReadPermission = ["users:read"];

    private sealed record RoleResponse(Guid Id, string Name, bool IsSystem, string[] Permissions);

    private sealed record UserResponse(Guid Id, string Email, string[] Roles);

    [Fact]
    public async Task GetRoles_Should_ReturnForbidden_WhenUserHasNoPermission()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("roles");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRoles_Should_ReturnSeededAdministratorRole()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        List<RoleResponse>? roles = await HttpClient.GetFromJsonAsync<List<RoleResponse>>("roles");

        // Assert
        RoleResponse administrator = roles!.Single(r => r.Name == "Administrator");
        administrator.IsSystem.ShouldBeTrue();
        administrator.Permissions.ShouldContain("roles:manage");
    }

    [Fact]
    public async Task CreateRole_Should_ReturnProblem_WhenPermissionIsUnknown()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "roles",
            new { name = $"role-{Guid.NewGuid():N}", permissions = UnknownPermissions });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AssignedRole_Should_GrantItsPermissionsToTheUser()
    {
        // Arrange
        (Guid userId, AccessTokens userTokens) = await RegisterAndLoginAsync();

        await AuthenticateAsAdminAsync();

        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync(
            "roles",
            new { name = $"viewer-{Guid.NewGuid():N}", permissions = UsersReadPermission });
        createResponse.EnsureSuccessStatusCode();
        Guid roleId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage assignResponse = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/roles",
            new { roleIds = new[] { roleId } });
        assignResponse.EnsureSuccessStatusCode();

        // Act
        Authenticate(userTokens.AccessToken);
        HttpResponseMessage response = await HttpClient.GetAsync($"users/{userId}");

        // Assert
        response.EnsureSuccessStatusCode();
        UserResponse? user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user!.Roles.Length.ShouldBe(1);
    }

    [Fact]
    public async Task CreateRole_Should_ReplayResponse_WhenIdempotencyKeyIsRepeated()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string idempotencyKey = Guid.NewGuid().ToString();
        var body = new { name = $"role-{Guid.NewGuid():N}", permissions = Array.Empty<string>() };

        // Act
        Guid first = await PostWithKeyAsync(body, idempotencyKey);
        Guid second = await PostWithKeyAsync(body, idempotencyKey);

        // Assert
        second.ShouldBe(first);
    }

    private async Task<Guid> PostWithKeyAsync(object body, string idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "roles") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        HttpResponseMessage response = await HttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }
}
