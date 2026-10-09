using System.Net;
using System.Net.Http.Json;
using Domain.Roles;

namespace IntegrationTests.Users;

/// <summary>
/// The signed-in user's own session endpoints used by the mobile app (PLAN-MOBILE M0).
/// </summary>
public sealed class CurrentUserTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record RoleResponse(Guid Id, string Name, bool IsSystem, string[] Permissions);

    private sealed record MeResponse(Guid Id, string Email, string[] Roles, string[] Permissions);

    [Fact]
    public async Task SeededMobileRoles_Should_HaveTheirDefaultPermissions()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        List<RoleResponse>? roles = await HttpClient.GetFromJsonAsync<List<RoleResponse>>("roles");

        // Assert
        RoleResponse fieldOfficer = roles!.Single(r => r.Name == MobileRoles.FieldOfficer);
        fieldOfficer.IsSystem.ShouldBeFalse();
        fieldOfficer.Permissions.ShouldContain(Permissions.PartnershipAssignedOnly);
        fieldOfficer.Permissions.ShouldContain(Permissions.ProductionRecord);
        fieldOfficer.Permissions.ShouldNotContain(Permissions.ApprovalsDecide);

        RoleResponse manager = roles!.Single(r => r.Name == MobileRoles.Manager);
        manager.Permissions.ShouldContain(Permissions.ApprovalsDecide);
        manager.Permissions.ShouldNotContain(Permissions.PartnershipAssignedOnly);
        manager.Permissions.ShouldNotContain(Permissions.FarmersManage);
    }

    [Fact]
    public async Task Me_Should_ReturnRolesAndPermissionsOfTheSignedInUser()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);

        await AuthenticateAsAdminAsync();
        List<RoleResponse>? roles = await HttpClient.GetFromJsonAsync<List<RoleResponse>>("roles");
        RoleResponse fieldOfficer = roles!.Single(r => r.Name == MobileRoles.FieldOfficer);
        (await HttpClient.PutAsJsonAsync($"users/{userId}/roles", new { roleIds = new[] { fieldOfficer.Id } }))
            .EnsureSuccessStatusCode();

        AccessTokens tokens = await LoginAsync(email);
        Authenticate(tokens.AccessToken);

        // Act
        MeResponse? me = await HttpClient.GetFromJsonAsync<MeResponse>("users/me");

        // Assert
        me!.Id.ShouldBe(userId);
        me.Email.ShouldBe(email);
        me.Roles.ShouldBe([MobileRoles.FieldOfficer]);
        me.Permissions.ShouldBe(fieldOfficer.Permissions.Order(StringComparer.Ordinal), ignoreOrder: true);
    }

    [Fact]
    public async Task Me_Should_ReturnUnauthorized_WithoutToken()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Should_RevokeTheRefreshTokenOfThatDevice()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        AccessTokens deviceA = await LoginAsync(email);
        AccessTokens deviceB = await LoginAsync(email);
        Authenticate(deviceA.AccessToken);

        // Act
        HttpResponseMessage logout = await HttpClient.PostAsJsonAsync("users/logout", new { refreshToken = deviceA.RefreshToken });

        // Assert
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await HttpClient.PostAsJsonAsync("users/refresh-token", new { refreshToken = deviceA.RefreshToken }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await HttpClient.PostAsJsonAsync("users/refresh-token", new { refreshToken = deviceB.RefreshToken }))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ChangePassword_Should_RequireTheNewPasswordAndEndOtherSessions()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);
        Authenticate(tokens.AccessToken);
        const string newPassword = "NewPassword456";

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/me/change-password",
            new { currentPassword = Password, newPassword });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await HttpClient.PostAsJsonAsync("users/refresh-token", new { refreshToken = tokens.RefreshToken }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await HttpClient.PostAsJsonAsync("users/login", new { email, password = Password }))
            .IsSuccessStatusCode.ShouldBeFalse();
        (await LoginAsync(email, newPassword)).AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnProblem_WhenTheCurrentPasswordIsWrong()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/me/change-password",
            new { currentPassword = "WrongPassword1", newPassword = "NewPassword456" });

        // Assert
        response.IsSuccessStatusCode.ShouldBeFalse();
        (await response.Content.ReadAsStringAsync()).ShouldContain("Users.InvalidCurrentPassword");
    }
}
