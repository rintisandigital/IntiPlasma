using System.Net;
using MobileApp.Core.Api;
using MobileApp.Core.Contracts;
using MobileApp.Core.Local;
using MobileApp.Core.Session;

namespace MobileApp.UnitTests.Session;

public sealed class SessionServiceTests : IDisposable
{
    private static readonly CurrentUser Ppl = new()
    {
        Id = Guid.CreateVersion7(),
        Email = "ppl@intiplasma.test",
        FirstName = "Budi",
        LastName = "Santoso",
        Roles = ["PPL"],
        Permissions = [AppPermissions.FarmersRead, AppPermissions.ProductionRecord]
    };

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"intiplasma-session-{Guid.NewGuid():N}.db3");
    private readonly InMemorySecureStore _secureStore = new();
    private readonly TestSettings _settings = new();
    private readonly List<IDisposable> _resources = [];

    private bool _online = true;
    private CurrentUser _me = Ppl;
    private string _validPassword = "Password123";

    [Fact]
    public async Task LoginAsync_Should_StoreTokensAndTheProfile()
    {
        // Arrange
        SessionService session = await CreateAsync();

        // Act
        ApiResult result = await session.LoginAsync(" ppl@intiplasma.test ", "Password123");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session.State.ShouldBe(SessionState.SignedIn);
        session.User!.Email.ShouldBe(Ppl.Email);
        session.Has(AppPermissions.ProductionRecord).ShouldBeTrue();
        _secureStore.Values[TokenStore.RefreshTokenKey].ShouldBe("refresh-1");
        _settings.LastEmail.ShouldBe(Ppl.Email);
    }

    [Fact]
    public async Task LoginAsync_Should_ReturnTheServerMessage_WhenThePasswordIsWrong()
    {
        // Arrange
        SessionService session = await CreateAsync();

        // Act
        ApiResult result = await session.LoginAsync(Ppl.Email, "wrong");

        // Assert
        result.Error!.Message.ShouldBe("Email atau password salah.");
        session.State.ShouldBe(SessionState.SignedOut);
        _secureStore.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoginAsync_Should_RefuseAUserWithoutPermissions()
    {
        // Arrange
        _me = Ppl with { Permissions = [] };
        SessionService session = await CreateAsync();

        // Act
        ApiResult result = await session.LoginAsync(Ppl.Email, "Password123");

        // Assert
        result.Error!.Code.ShouldBe("Client.NoMobileAccess");
        session.IsSignedIn.ShouldBeFalse();
        _secureStore.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_Should_RestoreTheStoredSession_EvenOffline()
    {
        // Arrange
        SessionService first = await CreateAsync();
        await first.LoginAsync(Ppl.Email, "Password123");
        _online = false;

        // Act
        SessionService restarted = await CreateAsync();

        // Assert
        restarted.State.ShouldBe(SessionState.SignedIn);
        restarted.User!.Id.ShouldBe(Ppl.Id);
    }

    [Fact]
    public async Task LogoutAsync_Should_ForgetTheSessionOnTheDevice_EvenOffline()
    {
        // Arrange
        SessionService session = await CreateAsync();
        await session.LoginAsync(Ppl.Email, "Password123");
        _online = false;

        // Act
        await session.LogoutAsync();
        SessionService restarted = await CreateAsync();

        // Assert
        session.State.ShouldBe(SessionState.SignedOut);
        _secureStore.Values.ShouldBeEmpty();
        restarted.State.ShouldBe(SessionState.SignedOut);
    }

    [Fact]
    public async Task ChangePasswordAsync_Should_SignInAgainWithTheNewPassword()
    {
        // Arrange
        SessionService session = await CreateAsync();
        await session.LoginAsync(Ppl.Email, "Password123");

        // Act
        ApiResult result = await session.ChangePasswordAsync("Password123", "NewPassword456");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session.IsSignedIn.ShouldBeTrue();
        _secureStore.Values[TokenStore.RefreshTokenKey].ShouldBe("refresh-2");
    }

    [Fact]
    public async Task SessionExpired_Should_SignOut()
    {
        // Arrange
        (SessionService session, TokenRefresher refresher) = await CreateWithRefresherAsync();
        await session.LoginAsync(Ppl.Email, "Password123");

        // Act
        await refresher.RefreshAsync("access-1", CancellationToken.None);

        // Assert
        session.State.ShouldBe(SessionState.SignedOut);
        session.SessionExpired.ShouldBeTrue();
    }

    public void Dispose()
    {
        _resources.ForEach(r => r.Dispose());
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    private async Task<SessionService> CreateAsync() => (await CreateWithRefresherAsync()).Session;

    /// <summary>
    /// Session over a fake Web.Api: login accepts <see cref="_validPassword"/>, change-password swaps it, every
    /// refresh is rejected, and nothing answers while <see cref="_online"/> is false.
    /// </summary>
    private async Task<(SessionService Session, TokenRefresher Refresher)> CreateWithRefresherAsync()
    {
        int logins = 0;

        HttpResponseMessage Respond(HttpRequestMessage request, string? body)
        {
            if (!_online)
            {
                throw new HttpRequestException("offline");
            }

            switch (request.RequestUri!.AbsolutePath)
            {
                case "/api/v1/users/login" when body!.Contains(_validPassword, StringComparison.Ordinal):
                    logins++;
                    return StubHttpHandler.Json(new AccessTokens($"access-{logins}", $"refresh-{logins}"));
                case "/api/v1/users/login":
                    return StubHttpHandler.Problem(HttpStatusCode.NotFound, "Users.NotFoundByEmail", "Not found");
                case "/api/v1/users/me":
                    return StubHttpHandler.Json(_me);
                case "/api/v1/users/me/change-password":
                    _validPassword = "NewPassword456";
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                case "/api/v1/users/refresh-token":
                    return StubHttpHandler.Problem(HttpStatusCode.BadRequest, "Users.InvalidRefreshToken", "Invalid");
                default:
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        }

        var api = new FakeApi(Respond, secureStore: _secureStore, settings: _settings);
        var db = new LocalDb(_dbPath);
        _resources.Add(api);
        _resources.Add(db);

        var session = new SessionService(new UsersApi(api.Client), api.Tokens, api.Refresher, db, _settings);
        await session.InitializeAsync();

        return (session, api.Refresher);
    }
}
