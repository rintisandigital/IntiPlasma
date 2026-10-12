using System.Net;
using System.Text.RegularExpressions;
using Domain.Approvals;
using Domain.MasterData;
using Domain.Partnership.Contracts;
using Infrastructure.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Web.App.IntegrationTests;

/// <summary>
/// PLAN-MOBILE M6 in the WebApp: approval flows, inbox, submissions, delegations, replacing an approver and the
/// branch set-up.
/// </summary>
[Collection(nameof(WebAppCollection))]
public sealed partial class ApprovalTests(WebAppFactory factory)
{
    public static TheoryData<string> Pages =>
    [
        "/Approvals/Inbox", "/Approvals/Requests", "/Approvals/Requests?stuck=true", "/Approvals/Delegations",
        "/Approvals/MySubmissions", "/Approvals/MyDelegations", "/Admin/ApprovalFlows", "/Admin/ApprovalFlows/Create",
        "/Admin/ApprovalFlows/ReplaceApprover", "/Admin/Branches/Create"
    ];

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Admin_Should_OpenPage(string path)
    {
        HttpClient client = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);

        (await client.GetAsync(new Uri(path, UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Farmer_Should_BeApprovedFromTheInbox_ByTheApproverOnly()
    {
        HttpClient admin = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        string s = Code();
        Guid branchId = await BranchAsync(admin, s);

        HttpResponseMessage created = await PostAsync(admin, "/Partnership/Farmers/Create", new()
        {
            ["Code"] = $"F{s}", ["Name"] = "Peternak Inbox", ["Type"] = "Inti", ["BranchId"] = branchId.ToString()
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        Guid farmerId = await QueryAsync(db => db.Farmers.Where(f => f.Code == $"F{s}").Select(f => f.Id).SingleAsync());
        created.Headers.Location!.ToString().ShouldContain(farmerId.ToString());

        string details = await admin.GetStringAsync(new Uri($"/Partnership/Farmers/Details/{farmerId}", UriKind.Relative));
        details.ShouldContain("Waiting for approval");
        details.ShouldContain("Cancel request");
        Guid requestId = await QueryAsync(db => db.ApprovalRequests.Where(r => r.DocumentId == farmerId).Select(r => r.Id).SingleAsync());

        // The requester sees the request but cannot decide it.
        (await admin.GetStringAsync(new Uri($"/Approvals/Inbox/Details/{requestId}", UriKind.Relative))).ShouldNotContain("Your decision");
        await PostAsync(admin, $"/Approvals/Inbox/Approve/{requestId}", []);
        (await StatusAsync(farmerId)).ShouldBe(RegistrationStatus.PendingApproval);

        HttpClient approver = await ClientAsync(WebAppFactory.ApproverEmail, WebAppFactory.ApproverPassword);
        string inbox = await approver.GetStringAsync(new Uri("/Approvals/Inbox", UriKind.Relative));
        inbox.ShouldContain($"F{s}");
        string decision = await approver.GetStringAsync(new Uri($"/Approvals/Inbox/Details/{requestId}", UriKind.Relative));
        decision.ShouldContain("Your decision");

        (await PostAsync(approver, $"/Approvals/Inbox/Approve/{requestId}", new() { ["note"] = "OK" })).StatusCode.ShouldBe(HttpStatusCode.Redirect);

        (await StatusAsync(farmerId)).ShouldBe(RegistrationStatus.Approved);
        (await admin.GetStringAsync(new Uri($"/Partnership/Farmers/Details/{farmerId}", UriKind.Relative))).ShouldContain("“OK”");
    }

    [Fact]
    public async Task Requester_Should_CancelFromTheDocument_AndSubmitAgain()
    {
        HttpClient admin = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        string s = Code();
        Guid branchId = await BranchAsync(admin, s);
        await PostAsync(admin, "/Partnership/Farmers/Create", new()
        {
            ["Code"] = $"F{s}", ["Name"] = "Peternak Batal", ["Type"] = "Inti", ["BranchId"] = branchId.ToString()
        });
        Guid farmerId = await QueryAsync(db => db.Farmers.Where(f => f.Code == $"F{s}").Select(f => f.Id).SingleAsync());
        Guid requestId = await QueryAsync(db => db.ApprovalRequests.Where(r => r.DocumentId == farmerId).Select(r => r.Id).SingleAsync());

        (await admin.GetStringAsync(new Uri("/Approvals/MySubmissions", UriKind.Relative))).ShouldContain($"F{s}");
        await PostAsync(admin, $"/Approvals/MySubmissions/Cancel/{requestId}", new() { ["returnUrl"] = $"/Partnership/Farmers/Edit/{farmerId}" });
        (await StatusAsync(farmerId)).ShouldBe(RegistrationStatus.Draft);

        await PostAsync(admin, $"/Partnership/Farmers/Submit/{farmerId}", []);
        (await StatusAsync(farmerId)).ShouldBe(RegistrationStatus.PendingApproval);
    }

    [Fact]
    public async Task Contract_Should_BeSubmitted_InsteadOfActivated()
    {
        HttpClient admin = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        string s = Code();
        Guid branchId = await BranchAsync(admin, s);
        PartnershipContract contract = PartnershipContract.Create($"K{s}", branchId, ContractScheme.ProfitSharing, new ContractTerms(
            "Bagi hasil", DateOnly.FromDateTime(DateTime.Today), null, 40m, null, null, [], [], [])).Value;
        await QueryAsync(async db =>
        {
            db.Contracts.Add(contract);
            return await db.SaveChangesAsync();
        });

        string details = await admin.GetStringAsync(new Uri($"/Partnership/Contracts/Details/{contract.Id}", UriKind.Relative));
        details.ShouldContain("Submit for approval");
        details.ShouldNotContain(">Activate<");

        await PostAsync(admin, $"/Partnership/Contracts/Submit/{contract.Id}", []);
        (await QueryAsync(db => db.Contracts.Where(c => c.Id == contract.Id).Select(c => c.Status).SingleAsync())).ShouldBe(ContractStatus.PendingApproval);

        await factory.ApproveAsync(contract.Id);
        (await QueryAsync(db => db.Contracts.Where(c => c.Id == contract.Id).Select(c => c.Status).SingleAsync())).ShouldBe(ContractStatus.Active);
    }

    [Fact]
    public async Task ApprovalFlow_Should_BeCreatedAndEdited_WithDynamicLevels()
    {
        HttpClient admin = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        string s = Code();
        Guid branchId = await BranchAsync(admin, s);
        string approver = factory.ApproverId.ToString();

        HttpResponseMessage created = await PostAsync(admin, "/Admin/ApprovalFlows/Create", new()
        {
            ["DocumentType"] = "Coop", ["BranchId"] = branchId.ToString(), ["Name"] = $"Coop {s}",
            ["Levels[0].Name"] = "Verifikasi", ["Levels[0].ApproverUserIds"] = approver
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        Guid flowId = await QueryAsync(db => db.ApprovalFlows.Where(f => f.BranchId == branchId).Select(f => f.Id).SingleAsync());

        (await admin.GetStringAsync(new Uri($"/Admin/ApprovalFlows/Edit/{flowId}", UriKind.Relative))).ShouldContain("Verifikasi");
        await PostAsync(admin, "/Admin/ApprovalFlows/Edit", new()
        {
            ["Id"] = flowId.ToString(), ["DocumentType"] = "Coop", ["BranchId"] = branchId.ToString(), ["Name"] = $"Coop {s}", ["IsActive"] = "true",
            ["Levels[0].Name"] = "Verifikasi", ["Levels[0].ApproverUserIds"] = approver,
            ["Levels[1].Name"] = "Kepala Cabang", ["Levels[1].ApproverUserIds"] = approver
        });
        ApprovalFlow flow = await QueryAsync(db => db.ApprovalFlows.Include(f => f.Levels).AsNoTracking().SingleAsync(f => f.Id == flowId));
        flow.Levels.Select(l => l.Name).ShouldBe(["Verifikasi", "Kepala Cabang"]);

        // A second active flow for the same type and branch is refused.
        HttpResponseMessage duplicate = await PostAsync(admin, "/Admin/ApprovalFlows/Create", new()
        {
            ["DocumentType"] = "Coop", ["BranchId"] = branchId.ToString(),
            ["Levels[0].Name"] = "Verifikasi", ["Levels[0].ApproverUserIds"] = approver
        });
        duplicate.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await duplicate.Content.ReadAsStringAsync()).ShouldContain("already exists");
    }

    [Fact]
    public async Task NewBranch_Should_GetItsOwnFlows_FromTheBranchForm()
    {
        HttpClient admin = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        string s = Code();

        (await admin.GetStringAsync(new Uri("/Admin/Branches/Create", UriKind.Relative))).ShouldContain("Approval flow");
        await PostAsync(admin, "/Admin/Branches/Create", new()
        {
            ["Code"] = $"N{s}", ["Name"] = $"Branch {s}", ["ApprovalSetup"] = "branch",
            ["ApprovalDocumentTypes"] = "Farmer",
            ["ApprovalLevels[0].Name"] = "Kepala Cabang", ["ApprovalLevels[0].ApproverUserIds"] = factory.ApproverId.ToString(),
            ["GrantBranchAccess"] = "true"
        });

        Guid branchId = await QueryAsync(db => db.Branches.Where(b => b.Code == $"N{s}").Select(b => b.Id).SingleAsync());
        (await QueryAsync(db => db.ApprovalFlows.CountAsync(f => f.BranchId == branchId))).ShouldBe(1);
        (await admin.GetStringAsync(new Uri($"/Admin/Branches/Edit/{branchId}", UriKind.Relative))).ShouldContain("Branch flow");
    }

    [Fact]
    public async Task Delegation_And_ReplaceApprover_Should_BeManagedFromTheWebApp()
    {
        HttpClient approver = await ClientAsync(WebAppFactory.ApproverEmail, WebAppFactory.ApproverPassword);
        string today = DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        Guid delegateId = await QueryAsync(db => db.Users.Where(u => u.Email == WebAppFactory.AdminEmail).Select(u => u.Id).SingleAsync());

        await PostAsync(approver, "/Approvals/MyDelegations/Create", new()
        {
            ["Form.DelegateUserId"] = delegateId.ToString(), ["Form.FromDate"] = today, ["Form.ToDate"] = today, ["Form.Reason"] = "Cuti"
        });
        ApprovalDelegation delegation = await QueryAsync(db => db.ApprovalDelegations.AsNoTracking().SingleAsync(d => d.DelegatorUserId == factory.ApproverId));
        (await approver.GetStringAsync(new Uri("/Approvals/MyDelegations", UriKind.Relative))).ShouldContain("Cuti");

        await PostAsync(approver, $"/Approvals/MyDelegations/Revoke/{delegation.Id}", []);
        (await QueryAsync(db => db.ApprovalDelegations.Where(d => d.Id == delegation.Id).Select(d => d.RevokedAtUtc).SingleAsync())).ShouldNotBeNull();

        HttpClient admin = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        string preview = await admin.GetStringAsync(new Uri($"/Admin/ApprovalFlows/ReplaceApprover?fromUserId={factory.ApproverId}", UriKind.Relative));
        preview.ShouldContain("Where this user is an approver");
        (await admin.GetStringAsync(new Uri($"/Admin/Users/Details/{factory.ApproverId}", UriKind.Relative))).ShouldContain("Replace approver");
    }

    private static string Code() => Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    private async Task<Guid> BranchAsync(HttpClient client, string s)
    {
        await PostAsync(client, "/Admin/Branches/Create", new() { ["Code"] = $"B{s}", ["Name"] = $"Branch {s}" });

        return await QueryAsync(db => db.Branches.Where(b => b.Code == $"B{s}").Select(b => b.Id).SingleAsync());
    }

    private Task<RegistrationStatus> StatusAsync(Guid farmerId) =>
        QueryAsync(db => db.Farmers.Where(f => f.Id == farmerId).Select(f => f.RegistrationStatus).SingleAsync());

    private async Task<T> QueryAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, Dictionary<string, string> fields)
    {
        string page = await client.GetStringAsync(new Uri("/Approvals/MyDelegations", UriKind.Relative));
        fields["__RequestVerificationToken"] = AntiforgeryToken(page);

        using var content = new FormUrlEncodedContent(fields);
        return await client.PostAsync(new Uri(path, UriKind.Relative), content);
    }

    private async Task<HttpClient> ClientAsync(string email, string password)
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        string page = await client.GetStringAsync(new Uri("/Auth/Login", UriKind.Relative));

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        });
        (await client.PostAsync(new Uri("/Auth/Login", UriKind.Relative), form)).StatusCode.ShouldBe(HttpStatusCode.Redirect);

        return client;
    }

    private static string AntiforgeryToken(string html) => TokenRegex().Match(html).Groups[1].Value;

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
