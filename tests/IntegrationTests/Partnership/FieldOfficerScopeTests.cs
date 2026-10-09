using System.Net;
using System.Net.Http.Json;
using Domain.Roles;

namespace IntegrationTests.Partnership;

/// <summary>
/// PPL data scope (PLAN-MOBILE M1, M-36 s.d. M-39): a PPL only sees the farmers and coops assigned to them,
/// a Manager sees the whole branch, and a reassignment applies right away.
/// </summary>
public sealed class FieldOfficerScopeTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record RoleResponse(Guid Id, string Name);

    private sealed record Item(Guid Id, string Code, Guid? FieldOfficerUserId);

    private sealed record Page(List<Item> Items, long TotalCount);

    private sealed record Officer(Guid Id, string Email);

    [Fact]
    public async Task Ppl_Should_SeeOnlyAssignedData_AndManager_TheWholeBranch()
    {
        Setup s = await SetupAsync();

        // PPL A: farmer A (direct), farmer C (through coop C), coops A and C.
        Authenticate(s.PplA);
        (await CodesAsync($"farmers?branchId={s.Branch}")).ShouldBe([s.Code("FA"), s.Code("FC")], ignoreOrder: true);
        (await CodesAsync($"coops?branchId={s.Branch}")).ShouldBe([s.Code("CA"), s.Code("CC")], ignoreOrder: true);
        (await HttpClient.GetAsync($"farmers/{s.FarmerB}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await HttpClient.GetAsync($"coops/{s.CoopB}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await HttpClient.GetAsync($"coops/{s.CoopA}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // PPL B: only its own farmer and coop.
        Authenticate(s.PplB);
        (await CodesAsync($"farmers?branchId={s.Branch}")).ShouldBe([s.Code("FB")]);
        (await CodesAsync($"coops?branchId={s.Branch}")).ShouldBe([s.Code("CB")]);
        (await HttpClient.GetAsync($"farmers/{s.FarmerA}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Manager: everything in the branch, filterable per PPL.
        Authenticate(s.Manager);
        (await CodesAsync($"farmers?branchId={s.Branch}")).Count.ShouldBe(3);
        (await CodesAsync($"coops?branchId={s.Branch}")).Count.ShouldBe(3);
        (await CodesAsync($"coops?branchId={s.Branch}&fieldOfficerId={s.PplBId}")).ShouldBe([s.Code("CB")]);
        (await CodesAsync($"farmers?branchId={s.Branch}&fieldOfficerId={s.PplAId}"))
            .ShouldBe([s.Code("FA"), s.Code("FC")], ignoreOrder: true);
    }

    [Fact]
    public async Task Reassignment_Should_MoveTheCoopToTheNewPpl_WithoutNewLogin()
    {
        Setup s = await SetupAsync();

        await AuthenticateAsAdminAsync();
        (await HttpClient.PutAsJsonAsync($"coops/{s.CoopB}", new
        {
            name = "Kandang B",
            capacity = 5_000,
            houseType = "ClosedHouse",
            isActive = true,
            fieldOfficerUserId = s.PplAId
        })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Authenticate(s.PplA);
        (await HttpClient.GetAsync($"coops/{s.CoopB}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CodesAsync($"farmers?branchId={s.Branch}")).ShouldContain(s.Code("FB"));

        Authenticate(s.PplB);
        (await HttpClient.GetAsync($"coops/{s.CoopB}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        // Farmer B is still assigned to PPL B directly.
        (await CodesAsync($"farmers?branchId={s.Branch}")).ShouldBe([s.Code("FB")]);
    }

    [Fact]
    public async Task Ppl_Should_BeAssignedToTheFarmerTheyCreate()
    {
        Setup s = await SetupAsync();
        Authenticate(s.PplA);

        Guid farmerId = await PostAsync<Guid>("farmers", Farmer(s.Code("FN"), s.Branch, s.PplBId));

        Item? farmer = await HttpClient.GetFromJsonAsync<Item>($"farmers/{farmerId}");
        farmer!.FieldOfficerUserId.ShouldBe(s.PplAId);
    }

    [Fact]
    public async Task Assignment_Should_BeRejected_WhenUserIsNotAPpl()
    {
        Setup s = await SetupAsync();
        await AuthenticateAsAdminAsync();

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("farmers", Farmer(s.Code("FX"), s.Branch, s.ManagerId));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Users.NotFieldOfficer");
    }

    [Fact]
    public async Task FieldOfficers_Should_ListThePplOfTheBranch()
    {
        Setup s = await SetupAsync();
        Authenticate(s.Manager);

        List<Officer>? officers = await HttpClient.GetFromJsonAsync<List<Officer>>($"users/field-officers?branchId={s.Branch}");

        officers!.Select(o => o.Id).ShouldBe([s.PplAId, s.PplBId], ignoreOrder: true);
    }

    private sealed record Setup(
        string Suffix,
        Guid Branch,
        Guid PplAId,
        string PplA,
        Guid PplBId,
        string PplB,
        Guid ManagerId,
        string Manager,
        Guid FarmerA,
        Guid FarmerB,
        Guid CoopA,
        Guid CoopB)
    {
        public string Code(string name) => $"{name}{Suffix}";
    }

    private async Task<Setup> SetupAsync()
    {
        await AuthenticateAsAdminAsync();
        string suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        Guid branch = await PostAsync<Guid>("branches", new { code = $"P{suffix}", name = "Cabang PPL" });
        Guid profile = await PostAsync<Guid>("branch-access-profiles",
            new { name = $"Area {suffix}", allBranches = false, branchIds = new[] { branch } });
        List<RoleResponse>? roles = await HttpClient.GetFromJsonAsync<List<RoleResponse>>("roles");
        Guid pplRole = roles!.Single(r => r.Name == MobileRoles.FieldOfficer).Id;
        Guid managerRole = roles!.Single(r => r.Name == MobileRoles.Manager).Id;

        (Guid pplAId, string pplA) = await CreateUserAsync(pplRole, profile, branch);
        (Guid pplBId, string pplB) = await CreateUserAsync(pplRole, profile, branch);
        (Guid managerId, string manager) = await CreateUserAsync(managerRole, profile, branch);

        await AuthenticateAsAdminAsync();
        Guid farmerA = await PostAsync<Guid>("farmers", Farmer($"FA{suffix}", branch, pplAId));
        Guid farmerB = await PostAsync<Guid>("farmers", Farmer($"FB{suffix}", branch, pplBId));
        Guid farmerC = await PostAsync<Guid>("farmers", Farmer($"FC{suffix}", branch, null));
        Guid coopA = await PostAsync<Guid>("coops", Coop(farmerA, $"CA{suffix}", pplAId));
        Guid coopB = await PostAsync<Guid>("coops", Coop(farmerB, $"CB{suffix}", pplBId));
        await PostAsync<Guid>("coops", Coop(farmerC, $"CC{suffix}", pplAId));

        return new Setup(suffix, branch, pplAId, pplA, pplBId, pplB, managerId, manager, farmerA, farmerB, coopA, coopB);
    }

    private async Task<(Guid Id, string AccessToken)> CreateUserAsync(Guid roleId, Guid profileId, Guid branchId)
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);

        await AuthenticateAsAdminAsync();
        (await HttpClient.PutAsJsonAsync($"users/{userId}/roles", new { roleIds = new[] { roleId } })).EnsureSuccessStatusCode();
        (await HttpClient.PutAsJsonAsync($"users/{userId}/access", new { branchAccessProfileId = profileId, defaultBranchId = branchId }))
            .EnsureSuccessStatusCode();

        return (userId, (await LoginAsync(email)).AccessToken);
    }

    private static object Farmer(string code, Guid branchId, Guid? fieldOfficerUserId) => new
    {
        code,
        name = $"Peternak {code}",
        type = "Inti",
        branchId,
        taxIdentity = new { isPkp = false },
        bankAccount = new { },
        fieldOfficerUserId
    };

    private static object Coop(Guid farmerId, string code, Guid? fieldOfficerUserId) => new
    {
        farmerId,
        code,
        name = $"Kandang {code}",
        capacity = 5_000,
        houseType = "ClosedHouse",
        fieldOfficerUserId
    };

    private async Task<List<string>> CodesAsync(string url)
    {
        Page? page = await HttpClient.GetFromJsonAsync<Page>(url);

        return [.. page!.Items.Select(i => i.Code)];
    }

    private async Task<T> PostAsync<T>(string url, object body)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(url, body);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"POST {url} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
