using System.Net;
using MobileApp.Core.Api;
using MobileApp.Core.Contracts;
using MobileApp.Core.Local;

namespace MobileApp.UnitTests.Api;

/// <summary>
/// Partnership data with the read-only cache (PLAN-MOBILE M1, M-4).
/// </summary>
public sealed class PartnershipApiTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"intiplasma-mobile-{Guid.NewGuid():N}.db3");
    private bool _online = true;
    private HttpStatusCode _status = HttpStatusCode.OK;

    [Fact]
    public async Task GetFarmers_Should_SendTheQuery_AndReturnThePage()
    {
        // Arrange
        using var api = new FakeApi(Respond);
        using var db = new LocalDb(_path);
        var partnership = new PartnershipApi(new ApiCache(api.Client, db), api.Client);
        var officer = Guid.NewGuid();

        // Act
        CachedResult<PagedList<Farmer>> result = await partnership.GetFarmersAsync(null, 1, officer);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsFromCache.ShouldBeFalse();
        result.Value.Items.Single().Code.ShouldBe("PLM-001");
        result.Value.HasMore.ShouldBeTrue();
        api.Api.Requests.Single().Path.ShouldBe("/api/v1/farmers");
    }

    [Fact]
    public async Task Offline_Should_ReturnTheLastResponse_WithItsAge()
    {
        // Arrange
        using var api = new FakeApi(Respond);
        using var db = new LocalDb(_path);
        var partnership = new PartnershipApi(new ApiCache(api.Client, db), api.Client);
        await partnership.GetFarmersAsync(null, 1, null);

        _online = false;

        // Act
        CachedResult<PagedList<Farmer>> result = await partnership.GetFarmersAsync(null, 1, null);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsFromCache.ShouldBeTrue();
        result.CachedAtUtc!.Value.ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        result.Value.Items.Single().FieldOfficerName.ShouldBe("Budi PPL");
    }

    [Fact]
    public async Task Offline_Should_Fail_WhenNothingIsCached()
    {
        // Arrange
        using var api = new FakeApi(Respond);
        using var db = new LocalDb(_path);
        var partnership = new PartnershipApi(new ApiCache(api.Client, db), api.Client);
        _online = false;

        // Act
        CachedResult<PagedList<Farmer>> result = await partnership.GetFarmersAsync(null, 1, null);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error!.IsNetwork.ShouldBeTrue();
    }

    [Fact]
    public async Task NotFound_Should_DropTheCachedCopy_AndExplainTheScope()
    {
        // Arrange: the farmer was cached, then assigned to another PPL.
        using var api = new FakeApi(Respond);
        using var db = new LocalDb(_path);
        var partnership = new PartnershipApi(new ApiCache(api.Client, db), api.Client);
        var farmerId = Guid.NewGuid();
        await partnership.GetFarmerAsync(farmerId);
        _status = HttpStatusCode.NotFound;

        // Act
        CachedResult<Farmer> result = await partnership.GetFarmerAsync(farmerId);

        // Assert
        result.Error!.Message.ShouldBe("Peternak tidak ditemukan atau bukan tanggung jawab Anda.");
        (await db.GetCacheAsync($"farmers/{farmerId}")).ShouldBeNull();
    }

    [Fact]
    public async Task Searches_Should_NotBeCached()
    {
        // Arrange
        using var api = new FakeApi(Respond);
        using var db = new LocalDb(_path);
        var partnership = new PartnershipApi(new ApiCache(api.Client, db), api.Client);

        // Act
        await partnership.GetFarmersAsync("siti", 1, null);
        _online = false;
        CachedResult<PagedList<Farmer>> offline = await partnership.GetFarmersAsync("siti", 1, null);

        // Assert
        offline.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Path_Should_SkipEmptyValues_AndEscapeTheRest() =>
        PartnershipApi.Path("farmers", ("search", " a&b "), ("page", "2"), ("fieldOfficerId", null), ("type", ""))
            .ShouldBe("farmers?search=a%26b&page=2");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    private HttpResponseMessage Respond(HttpRequestMessage request, string? body)
    {
        if (!_online)
        {
            return StubHttpHandler.Offline(request, body);
        }

        if (_status == HttpStatusCode.NotFound)
        {
            return StubHttpHandler.Problem(HttpStatusCode.NotFound, "Farmers.NotFound", "The farmer was not found");
        }

        var farmer = new
        {
            id = Guid.NewGuid(),
            code = "PLM-001",
            name = "Siti",
            type = "Plasma",
            fieldOfficerName = "Budi PPL"
        };

        return request.RequestUri!.AbsolutePath.EndsWith("/farmers", StringComparison.Ordinal)
            ? StubHttpHandler.Json(new { items = new[] { farmer }, page = 1, pageSize = 1, totalCount = 3 })
            : StubHttpHandler.Json(farmer);
    }
}
