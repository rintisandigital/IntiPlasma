using System.Net;
using MobileApp.Core.Api;
using MobileApp.Core.Contracts;
using MobileApp.Core.Formatting;
using MobileApp.Core.Local;

namespace MobileApp.UnitTests.Api;

/// <summary>
/// Approval inbox, decisions and delegations (PLAN-MOBILE M6, M-92): readable offline, actions online only.
/// </summary>
public sealed class ApprovalsApiTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"intiplasma-mobile-{Guid.NewGuid():N}.db3");
    private bool _online = true;
    private Func<HttpRequestMessage, HttpResponseMessage>? _answer;

    [Fact]
    public async Task Inbox_Should_BeReadable_Offline()
    {
        using var api = new FakeApi(Respond);
        using var db = new LocalDb(_path);
        var approvals = new ApprovalsApi(new ApiCache(api.Client, db), api.Client);
        await approvals.GetInboxAsync("Farmer", 1);
        _online = false;

        CachedResult<PagedList<ApprovalRequest>> result = await approvals.GetInboxAsync("Farmer", 1);

        result.IsSuccess.ShouldBeTrue();
        result.IsFromCache.ShouldBeTrue();
        ApprovalRequest request = result.Value.Items.Single();
        Labels.ApprovalDocumentType(request.DocumentType).ShouldBe("Peternak baru");
        Labels.ApprovalLevel(request.Status, request.CurrentLevel, request.LevelCount, request.CurrentLevelName)
            .ShouldBe("Level 1 dari 2 · Manager Unit");
        api.Api.Requests[0].Path.ShouldBe("/api/v1/approvals/inbox");
    }

    [Fact]
    public async Task Approve_Should_FailOffline_WithTheNetworkMessage()
    {
        using var api = new FakeApi(Respond);
        using var db = new LocalDb(_path);
        var approvals = new ApprovalsApi(new ApiCache(api.Client, db), api.Client);
        _online = false;

        ApiResult result = await approvals.ApproveAsync(Guid.NewGuid(), "ok");

        result.Error!.IsNetwork.ShouldBeTrue();
        result.Error.Message.ShouldBe("Tidak dapat terhubung ke server. Periksa koneksi internet Anda.");
    }

    [Fact]
    public async Task Reject_Should_SendTheReason_AndMapTheServerError()
    {
        using var api = new FakeApi(Respond);
        using var db = new LocalDb(_path);
        var approvals = new ApprovalsApi(new ApiCache(api.Client, db), api.Client);
        var id = Guid.NewGuid();
        _answer = _ => StubHttpHandler.Problem(HttpStatusCode.Forbidden, "Approvals.NotApprover", "You are not an approver");

        ApiResult result = await approvals.RejectAsync(id, "  NIK salah  ");

        result.Error!.Message.ShouldBe("Anda bukan approver pada level yang sedang berjalan.");
        (HttpMethod method, string path, _, string? body, _) = api.Api.Requests.Single();
        method.ShouldBe(HttpMethod.Post);
        path.ShouldBe($"/api/v1/approvals/{id}/reject");
        body.ShouldBe("{\"reason\":\"NIK salah\"}");
    }

    [Fact]
    public async Task FlowNotConfigured_Should_ShowTheServerMessage()
    {
        using var api = new FakeApi(Respond);
        using var db = new LocalDb(_path);
        var approvals = new ApprovalsApi(new ApiCache(api.Client, db), api.Client);
        const string message = "Alur approval belum dikonfigurasi untuk kandang baru di cabang Cabang Bandung. Hubungi administrator.";
        _answer = _ => StubHttpHandler.Problem(HttpStatusCode.BadRequest, ErrorMessages.FlowNotConfiguredCode, message);

        ApiResult result = await approvals.CancelAsync(Guid.NewGuid());

        result.Error!.Message.ShouldBe(message);
    }

    [Fact]
    public async Task Delegate_Should_PostThePeriod_WithAnIdempotencyKey()
    {
        using var api = new FakeApi(Respond);
        using var db = new LocalDb(_path);
        var approvals = new ApprovalsApi(new ApiCache(api.Client, db), api.Client);
        var delegateId = Guid.NewGuid();
        _answer = _ => StubHttpHandler.Json(Guid.NewGuid());

        ApiResult<Guid> result = await approvals.DelegateAsync(delegateId, new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 15), "Cuti");

        result.IsSuccess.ShouldBeTrue();
        (_, string path, _, string? body, string? key) = api.Api.Requests.Single();
        path.ShouldBe("/api/v1/approvals/delegations");
        body!.ShouldContain("\"fromDate\":\"2026-10-12\"");
        key.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("PendingApproval", true, "Menunggu approval", Labels.Tone.Warning)]
    [InlineData("Rejected", false, "Ditolak", Labels.Tone.Danger)]
    [InlineData("Draft", false, "Draf", Labels.Tone.Info)]
    [InlineData("Approved", true, "Aktif", Labels.Tone.Success)]
    [InlineData("Approved", false, "Nonaktif", Labels.Tone.Neutral)]
    public void RecordStatus_Should_ShowTheApprovalState_UntilApproved(string status, bool active, string label, Labels.Tone tone)
    {
        Labels.RecordStatus(status, active).ShouldBe(label);
        Labels.RecordTone(status, active).ShouldBe(tone);
    }

    [Fact]
    public void FarmerChangeSummary_Should_NameTheFieldsInIndonesian()
    {
        Labels.ApprovalSummary("FarmerChange", "Nik, BankAccount").ShouldBe("NIK, Rekening bank");
        Labels.ApprovalSummary("Coop", "PTN-1 Iwan · 4,000").ShouldBe("PTN-1 Iwan · 4,000");
    }

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

        if (_answer is not null)
        {
            return _answer(request);
        }

        var item = new
        {
            id = Guid.NewGuid(),
            documentType = "Farmer",
            documentCode = "PTN-1",
            documentTitle = "Cecep",
            status = "Pending",
            currentLevel = 1,
            levelCount = 2,
            currentLevelName = "Manager Unit"
        };

        return StubHttpHandler.Json(new { items = new[] { item }, page = 1, pageSize = 20, totalCount = 1 });
    }
}
