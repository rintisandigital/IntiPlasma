using System.Net;
using MobileApp.Core.Api;
using MobileApp.Core.Contracts;
using MobileApp.Core.Local;
using MobileApp.Core.Session;

namespace MobileApp.UnitTests.Session;

public sealed class ProfilePhotoServiceTests : IDisposable
{
    private static readonly byte[] ServerPhoto = [0xFF, 0xD8, 0xFF, 0x01];
    private static readonly byte[] NewPhoto = [0xFF, 0xD8, 0xFF, 0x02];

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"intiplasma-photo-{Guid.NewGuid():N}.db3");
    private readonly List<IDisposable> _resources = [];

    private bool _online = true;
    private Guid? _photoId = Guid.CreateVersion7();
    private int _photoDownloads;

    [Fact]
    public async Task GetDataUrlAsync_Should_DownloadOnce_AndServeTheCacheOffline()
    {
        // Arrange
        (ProfilePhotoService photos, _) = await CreateAsync();

        // Act
        string? first = await photos.GetDataUrlAsync();
        _online = false;
        string? offline = await photos.GetDataUrlAsync();

        // Assert
        first.ShouldBe($"data:image/jpeg;base64,{Convert.ToBase64String(ServerPhoto)}");
        offline.ShouldBe(first);
        _photoDownloads.ShouldBe(1);
    }

    [Fact]
    public async Task GetDataUrlAsync_Should_ReturnNull_WhenTheUserHasNoPhoto()
    {
        // Arrange
        _photoId = null;
        (ProfilePhotoService photos, _) = await CreateAsync();

        // Act
        string? photo = await photos.GetDataUrlAsync();

        // Assert
        photo.ShouldBeNull();
        _photoDownloads.ShouldBe(0);
    }

    [Fact]
    public async Task UploadAsync_Should_RefreshTheProfile_AndCacheTheNewPhoto()
    {
        // Arrange
        (ProfilePhotoService photos, SessionService session) = await CreateAsync();
        int changes = 0;
        photos.Changed += (_, _) => changes++;

        // Act
        ApiResult result = await photos.UploadAsync(NewPhoto);
        string? shown = await photos.GetDataUrlAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session.User!.PhotoId.ShouldBe(_photoId);
        shown.ShouldBe($"data:image/jpeg;base64,{Convert.ToBase64String(NewPhoto)}");
        _photoDownloads.ShouldBe(0);
        changes.ShouldBe(1);
    }

    [Fact]
    public async Task UploadAsync_Should_ReturnTheIndonesianMessage_WhenTheServerRefusesTheFile()
    {
        // Arrange
        (ProfilePhotoService photos, SessionService session) = await CreateAsync();
        Guid? before = session.User!.PhotoId;

        // Act ("%PDF")
        ApiResult result = await photos.UploadAsync([0x25, 0x50, 0x44, 0x46]);

        // Assert
        result.Error!.Message.ShouldBe("Foto profil harus berupa gambar JPEG, PNG atau WEBP.");
        session.User!.PhotoId.ShouldBe(before);
    }

    [Fact]
    public async Task RemoveAsync_Should_ClearThePhoto()
    {
        // Arrange
        (ProfilePhotoService photos, SessionService session) = await CreateAsync();
        await photos.GetDataUrlAsync();

        // Act
        ApiResult result = await photos.RemoveAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session.User!.PhotoId.ShouldBeNull();
        (await photos.GetDataUrlAsync()).ShouldBeNull();
    }

    public void Dispose()
    {
        foreach (IDisposable resource in _resources)
        {
            resource.Dispose();
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    /// <summary>
    /// A signed-in session over a fake Web.Api that keeps one profile photo (<see cref="_photoId"/>): an upload of a
    /// JPEG replaces it, anything else is refused like the server does.
    /// </summary>
    private async Task<(ProfilePhotoService Photos, SessionService Session)> CreateAsync()
    {
        HttpResponseMessage Respond(HttpRequestMessage request, string? body)
        {
            if (!_online)
            {
                throw new HttpRequestException("offline");
            }

            switch ((request.Method.Method, request.RequestUri!.AbsolutePath))
            {
                case ("POST", "/api/v1/users/login"):
                    return StubHttpHandler.Json(new AccessTokens("access", "refresh"));
                case ("GET", "/api/v1/users/me"):
                    return StubHttpHandler.Json(new CurrentUser
                    {
                        Id = Guid.Empty,
                        Email = "ppl@intiplasma.test",
                        FirstName = "Budi",
                        PhotoId = _photoId,
                        Permissions = [AppPermissions.FarmersRead]
                    });
                case ("GET", "/api/v1/users/me/photo"):
                    _photoDownloads++;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(ServerPhoto) { Headers = { ContentType = new("image/jpeg") } }
                    };
                case ("POST", "/api/v1/users/me/photo") when body!.Contains("image/jpeg", StringComparison.Ordinal) && !body.Contains("%PDF", StringComparison.Ordinal):
                    _photoId = Guid.CreateVersion7();
                    return StubHttpHandler.Json(new UploadedPhoto(_photoId.Value));
                case ("POST", "/api/v1/users/me/photo"):
                    return StubHttpHandler.Problem(HttpStatusCode.BadRequest, "Users.PhotoNotImage", "Not an image");
                case ("DELETE", "/api/v1/users/me/photo"):
                    _photoId = null;
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                default:
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        var settings = new TestSettings();
        var api = new FakeApi(Respond, settings: settings);
        var db = new LocalDb(_dbPath);
        _resources.Add(api);
        _resources.Add(db);

        var users = new UsersApi(api.Client);
        var session = new SessionService(users, api.Tokens, api.Refresher, db, settings);
        await session.InitializeAsync();
        (await session.LoginAsync("ppl@intiplasma.test", "Password123")).IsSuccess.ShouldBeTrue();

        return (new ProfilePhotoService(users, session, db), session);
    }
}
