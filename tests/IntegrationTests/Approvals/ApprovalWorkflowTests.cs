using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Approvals;
using Domain.Roles;
using Infrastructure.Database;
using IntegrationTests.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Approvals;

/// <summary>
/// Multi-level approval through the API (PLAN-MOBILE §5, M6): a branch flow of two levels — Manager Unit, then
/// Kepala Cabang — for every document type.
/// </summary>
public sealed class ApprovalWorkflowTests(IntegrationTestWebAppFactory factory) : FieldScenarioTest(factory)
{
    private sealed record RoleResponse(Guid Id, string Name);

    private sealed record Users(
        Guid Branch,
        string Suffix,
        Guid PplId,
        string Ppl,
        Guid ManagerId,
        string Manager,
        string Manager2,
        Guid KacabId,
        string Kacab,
        Guid DelegateId,
        string Delegate);

    [Fact]
    public async Task Farmer_Should_BecomeActive_OnlyAfterBothLevelsApprove()
    {
        Users u = await SetUpApproversAsync();

        Authenticate(u.Ppl);
        Guid farmerId = await PostAsync<Guid>("farmers", Farmer($"AF{u.Suffix}", u.Branch));
        JsonElement farmer = await GetAsync($"farmers/{farmerId}");
        farmer.GetProperty("registrationStatus").GetString().ShouldBe("PendingApproval");
        farmer.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        farmer.GetProperty("approvalLevel").GetInt32().ShouldBe(1);
        Guid requestId = farmer.GetProperty("approvalRequestId").GetGuid();

        (await HttpClient.GetAsync("approvals/inbox")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await GetAsync("mobile/dashboard")).GetProperty("approvals").GetProperty("myPending").GetInt32().ShouldBe(1);

        Authenticate(u.Kacab);
        (await InboxIdsAsync()).ShouldNotContain(requestId);
        (await ApproveAsync(requestId)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        Authenticate(u.Manager);
        (await InboxIdsAsync()).ShouldContain(requestId);
        (await ApproveAsync(requestId, "Dokumen lengkap")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ApproveAsync(requestId)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        Authenticate(u.Kacab);
        JsonElement dashboard = await GetAsync("mobile/dashboard");
        dashboard.GetProperty("approvals").GetProperty("waitingForMe").GetInt32().ShouldBeGreaterThan(0);
        JsonElement count = await GetAsync("approvals/inbox/count");
        count.GetProperty("byDocumentType").GetProperty("Farmer").GetInt32().ShouldBeGreaterThan(0);
        (await ApproveAsync(requestId)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Authenticate(u.Ppl);
        farmer = await GetAsync($"farmers/{farmerId}");
        farmer.GetProperty("registrationStatus").GetString().ShouldBe("Approved");
        farmer.GetProperty("isActive").GetBoolean().ShouldBeTrue();

        JsonElement timeline = await GetAsync($"farmers/{farmerId}/approvals");
        JsonElement decisions = timeline[0].GetProperty("decisions");
        decisions.GetArrayLength().ShouldBe(2);
        decisions[0].GetProperty("note").GetString().ShouldBe("Dokumen lengkap");
    }

    [Fact]
    public async Task RejectedCoop_Should_BeSubmittedAgain_FromLevelOne_AndGetItsWarehouseWhenApproved()
    {
        Users u = await SetUpApproversAsync();
        Guid farmerId = await ApprovedFarmerAsync(u);

        Authenticate(u.Ppl);
        Guid coopId = await PostAsync<Guid>("coops", Coop(farmerId, $"AC{u.Suffix}", u.PplId));
        Guid requestId = (await PendingRequestAsync(coopId))!.Value;

        Authenticate(u.Manager);
        (await ApproveAsync(requestId)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Authenticate(u.Kacab);
        (await HttpClient.PostAsJsonAsync($"approvals/{requestId}/reject", new { reason = " " })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await HttpClient.PostAsJsonAsync($"approvals/{requestId}/reject", new { reason = "Izin lingkungan belum ada" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Authenticate(u.Ppl);
        JsonElement coop = await GetAsync($"coops/{coopId}");
        coop.GetProperty("registrationStatus").GetString().ShouldBe("Rejected");
        coop.GetProperty("approvalRejectReason").GetString().ShouldBe("Izin lingkungan belum ada");
        coop.GetProperty("warehouseId").ValueKind.ShouldBe(JsonValueKind.Null);

        JsonElement mine = await GetAsync("approvals/mine?status=Rejected");
        mine.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ShouldContain(requestId);

        Guid second = await PostAsync<Guid>($"coops/{coopId}/submit", new { });
        second.ShouldNotBe(requestId);
        JsonElement detail = await GetAsync($"approvals/{second}");
        detail.GetProperty("request").GetProperty("currentLevel").GetInt32().ShouldBe(1);
        detail.GetProperty("canCancel").GetBoolean().ShouldBeTrue();

        Authenticate(u.Manager);
        (await ApproveAsync(second)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Authenticate(u.Kacab);
        (await ApproveAsync(second)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await CoopWarehouseAsync(coopId)).ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Contract_Should_BeActivatedThroughApproval_Only()
    {
        Users u = await SetUpApproversAsync();

        await AuthenticateAsAdminAsync();
        Guid contractId = await PostAsync<Guid>("contracts", new
        {
            code = $"AK{u.Suffix}",
            branchId = u.Branch,
            scheme = "ProfitSharing",
            terms = new
            {
                name = "Bagi hasil",
                validFrom = Today,
                plasmaProfitSharePercent = 40m,
                inputPrices = Array.Empty<object>(),
                liveBirdPrices = Array.Empty<object>(),
                incentives = Array.Empty<object>()
            }
        });

        (await HttpClient.PostAsync($"contracts/{contractId}/activate", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        Guid requestId = await PostAsync<Guid>($"contracts/{contractId}/submit", new { });
        (await GetAsync($"contracts/{contractId}")).GetProperty("status").GetString().ShouldBe("PendingApproval");

        Authenticate(u.Manager);
        (await ApproveAsync(requestId)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Authenticate(u.Kacab);
        (await ApproveAsync(requestId)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await AuthenticateAsAdminAsync();
        (await GetAsync($"contracts/{contractId}")).GetProperty("status").GetString().ShouldBe("Active");
    }

    [Fact]
    public async Task FarmerChange_Should_KeepTheOldNik_UntilApproved()
    {
        Users u = await SetUpApproversAsync();
        Guid farmerId = await ApprovedFarmerAsync(u);

        Authenticate(u.Ppl);
        (await HttpClient.PutAsJsonAsync($"farmers/{farmerId}", new
        {
            name = "Peternak Approval",
            nik = "3201010101019999",
            taxIdentity = new { isPkp = false },
            bankAccount = new { bankName = "BRI", accountNumber = "123456", accountHolderName = "Peternak Approval" },
            isActive = true
        })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        JsonElement farmer = await GetAsync($"farmers/{farmerId}");
        farmer.GetProperty("nik").GetString().ShouldBe("3201010101010001");
        farmer.GetProperty("pendingChange").GetProperty("nik").GetString().ShouldBe("3201010101019999");
        farmer.GetProperty("approvalDocumentType").GetString().ShouldBe("FarmerChange");
        Guid requestId = farmer.GetProperty("approvalRequestId").GetGuid();

        Authenticate(u.Manager);
        JsonElement detail = await GetAsync($"approvals/{requestId}");
        detail.GetProperty("changes").EnumerateArray().Select(c => c.GetProperty("field").GetString()).ShouldBe(["Nik", "BankAccount"]);
        detail.GetProperty("canDecide").GetBoolean().ShouldBeTrue();
        (await ApproveAsync(requestId)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Authenticate(u.Kacab);
        (await ApproveAsync(requestId)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Authenticate(u.Ppl);
        farmer = await GetAsync($"farmers/{farmerId}");
        farmer.GetProperty("nik").GetString().ShouldBe("3201010101019999");
        farmer.GetProperty("pendingChange").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Requester_Should_CancelTheirPendingRequest()
    {
        Users u = await SetUpApproversAsync();

        Authenticate(u.Ppl);
        Guid farmerId = await PostAsync<Guid>("farmers", Farmer($"AX{u.Suffix}", u.Branch));
        Guid requestId = (await PendingRequestAsync(farmerId))!.Value;

        Authenticate(u.Manager);
        (await HttpClient.PostAsync($"approvals/{requestId}/cancel", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        Authenticate(u.Ppl);
        (await HttpClient.PostAsync($"approvals/{requestId}/cancel", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync($"farmers/{farmerId}")).GetProperty("registrationStatus").GetString().ShouldBe("Draft");
    }

    [Fact]
    public async Task ConcurrentDecisions_Should_LetOnlyOneApproverDecideALevel()
    {
        Users u = await SetUpApproversAsync();

        Authenticate(u.Ppl);
        Guid farmerId = await PostAsync<Guid>("farmers", Farmer($"AY{u.Suffix}", u.Branch));
        Guid requestId = (await PendingRequestAsync(farmerId))!.Value;

        using HttpClient other = Factory.CreateClient();
        other.BaseAddress = HttpClient.BaseAddress;
        other.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", u.Manager2);
        Authenticate(u.Manager);

        HttpResponseMessage[] responses = await Task.WhenAll(
            HttpClient.PostAsJsonAsync($"approvals/{requestId}/approve", new { }),
            other.PostAsJsonAsync($"approvals/{requestId}/approve", new { }));

        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
        responses.Count(r => r.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Forbidden).ShouldBe(1);
    }

    [Fact]
    public async Task A_Delegate_Should_DecideForTheApprover_WhileTheDelegationRuns()
    {
        Users u = await SetUpApproversAsync();

        Authenticate(u.Ppl);
        Guid farmerId = await PostAsync<Guid>("farmers", Farmer($"AD{u.Suffix}", u.Branch));
        Guid requestId = (await PendingRequestAsync(farmerId))!.Value;
        Authenticate(u.Manager);
        (await ApproveAsync(requestId)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Authenticate(u.Delegate);
        (await InboxIdsAsync()).ShouldNotContain(requestId);

        Authenticate(u.Kacab);
        Guid delegationId = await PostAsync<Guid>("approvals/delegations", new
        {
            delegateUserId = u.DelegateId,
            fromDate = Today,
            toDate = Today.AddDays(3),
            reason = "Cuti"
        });

        Authenticate(u.Delegate);
        (await InboxIdsAsync()).ShouldContain(requestId);
        JsonElement detail = await GetAsync($"approvals/{requestId}");
        detail.GetProperty("decidesOnBehalfOf").GetString().ShouldNotBeNullOrEmpty();
        (await ApproveAsync(requestId)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        detail = await GetAsync($"approvals/{requestId}");
        detail.GetProperty("decisions")[1].GetProperty("onBehalfOf").GetGuid().ShouldBe(u.KacabId);

        Authenticate(u.Kacab);
        (await HttpClient.PostAsync($"approvals/delegations/{delegationId}/revoke", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        JsonElement delegations = await GetAsync("approvals/delegations?includeEnded=true");
        delegations[0].GetProperty("status").GetString().ShouldBe("Revoked");
    }

    private async Task<Users> SetUpApproversAsync()
    {
        await AuthenticateAsAdminAsync();
        string suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        Guid branch = await PostAsync<Guid>("branches", new { code = $"A{suffix}", name = "Cabang Approval" });
        Guid profile = await PostAsync<Guid>("branch-access-profiles",
            new { name = $"Appr {suffix}", allBranches = false, branchIds = new[] { branch } });
        List<RoleResponse>? roles = await HttpClient.GetFromJsonAsync<List<RoleResponse>>("roles");
        Guid pplRole = roles!.Single(r => r.Name == MobileRoles.FieldOfficer).Id;
        Guid managerRole = roles!.Single(r => r.Name == MobileRoles.Manager).Id;

        (Guid pplId, string ppl) = await CreateUserAsync(pplRole, profile, branch);
        (Guid managerId, string manager) = await CreateUserAsync(managerRole, profile, branch);
        (Guid manager2Id, string manager2) = await CreateUserAsync(managerRole, profile, branch);
        (Guid kacabId, string kacab) = await CreateUserAsync(managerRole, profile, branch);
        (Guid delegateId, string delegateUser) = await CreateUserAsync(managerRole, profile, branch);

        using (IServiceScope scope = Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (ApprovalDocumentType documentType in Enum.GetValues<ApprovalDocumentType>())
            {
                db.ApprovalFlows.Add(ApprovalFlow.Create(
                    documentType,
                    branch,
                    $"{documentType} {suffix}",
                    [new ApprovalLevelDefinition("Manager Unit", [managerId, manager2Id]), new ApprovalLevelDefinition("Kepala Cabang", [kacabId])]).Value);
            }

            await db.SaveChangesAsync();
        }

        return new Users(branch, suffix, pplId, ppl, managerId, manager, manager2, kacabId, kacab, delegateId, delegateUser);
    }

    private async Task<Guid> ApprovedFarmerAsync(Users u)
    {
        Authenticate(u.Ppl);
        Guid farmerId = await PostAsync<Guid>("farmers", Farmer($"AP{u.Suffix}", u.Branch, "3201010101010001"));
        Guid requestId = (await PendingRequestAsync(farmerId))!.Value;

        Authenticate(u.Manager);
        (await ApproveAsync(requestId)).EnsureSuccessStatusCode();
        Authenticate(u.Kacab);
        (await ApproveAsync(requestId)).EnsureSuccessStatusCode();

        return farmerId;
    }

    private static object Farmer(string code, Guid branchId, string? nik = null) => new
    {
        code,
        name = "Peternak Approval",
        type = nik is null ? "Inti" : "Plasma",
        branchId,
        nik,
        taxIdentity = new { isPkp = false },
        bankAccount = new { }
    };

    private Task<HttpResponseMessage> ApproveAsync(Guid requestId, string? note = null) =>
        HttpClient.PostAsJsonAsync($"approvals/{requestId}/approve", new { note });

    private async Task<List<Guid>> InboxIdsAsync()
    {
        JsonElement inbox = await GetAsync("approvals/inbox?pageSize=100");

        return [.. inbox.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())];
    }

#pragma warning disable CA1054 // Relative API paths, as everywhere in these tests.
    private async Task<JsonElement> GetAsync(string url)
    {
        HttpResponseMessage response = await HttpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GET {url} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
#pragma warning restore CA1054
}
