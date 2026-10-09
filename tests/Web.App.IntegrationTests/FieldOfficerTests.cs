using System.Net;
using System.Text.RegularExpressions;
using Domain.Access;
using Domain.Auditing;
using Domain.Common;
using Domain.MasterData.Branches;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.Roles;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Web.App.IntegrationTests;

/// <summary>
/// PLAN-MOBILE M1: PPL on the Farmers/Farms pages and Partnership → Field Officer Assignment.
/// </summary>
[Collection(nameof(WebAppCollection))]
public sealed partial class FieldOfficerTests(WebAppFactory factory)
{
    [Fact]
    public async Task Assignment_Should_MoveSelectedRecords_AndWriteAuditLog()
    {
        Seed s = await SeedAsync();
        HttpClient client = await AdminClientAsync();

        string page = await client.GetStringAsync(new Uri($"/Partnership/FieldOfficers?branchId={s.BranchId}&from=none", UriKind.Relative));
        page.ShouldContain(s.FarmerCode);
        page.ShouldContain(s.CoopCode);
        page.ShouldContain("PPL A");

        HttpResponseMessage moved = await PostAsync(client, "/Partnership/FieldOfficers/Move",
        [
            new("branchId", s.BranchId.ToString()),
            new("from", "none"),
            new("to", s.PplA.ToString()),
            new("farmerIds", s.FarmerId.ToString()),
            new("coopIds", s.CoopId.ToString())
        ]);
        moved.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        (await QueryAsync(db => db.Farmers.Where(f => f.Id == s.FarmerId).Select(f => f.FieldOfficerUserId).SingleAsync())).ShouldBe(s.PplA);
        (await QueryAsync(db => db.Coops.Where(c => c.Id == s.CoopId).Select(c => c.FieldOfficerUserId).SingleAsync())).ShouldBe(s.PplA);
        (await QueryAsync(db => db.AuditLogs.AnyAsync(a => a.Category == AuditCategory.Access && a.Action == "ReassignFieldOfficer"
                                                         && a.EntityId == s.PplA)))
            .ShouldBeTrue();

        // From PPL A to PPL B: only the farm.
        await PostAsync(client, "/Partnership/FieldOfficers/Move",
        [
            new("branchId", s.BranchId.ToString()),
            new("from", s.PplA.ToString()),
            new("to", s.PplB.ToString()),
            new("coopIds", s.CoopId.ToString())
        ]);

        (await QueryAsync(db => db.Coops.Where(c => c.Id == s.CoopId).Select(c => c.FieldOfficerUserId).SingleAsync())).ShouldBe(s.PplB);
        (await QueryAsync(db => db.Farmers.Where(f => f.Id == s.FarmerId).Select(f => f.FieldOfficerUserId).SingleAsync())).ShouldBe(s.PplA);

        string coops = await client.GetStringAsync(new Uri($"/Partnership/Coops?branch={s.BranchId}&fieldOfficerId={s.PplB}", UriKind.Relative));
        coops.ShouldContain(s.CoopCode);
        coops.ShouldContain("PPL B");
    }

    [Fact]
    public async Task FarmerForm_Should_SaveTheFieldOfficer()
    {
        Seed s = await SeedAsync();
        HttpClient client = await AdminClientAsync();

        HttpResponseMessage saved = await PostAsync(client, "/Partnership/Farmers/Edit",
        [
            new("Id", s.FarmerId.ToString()),
            new("Code", s.FarmerCode),
            new("Type", "Inti"),
            new("BranchId", s.BranchId.ToString()),
            new("Name", "Peternak Inti"),
            new("IsActive", "true"),
            new("FieldOfficerUserId", s.PplB.ToString())
        ]);
        saved.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        (await QueryAsync(db => db.Farmers.Where(f => f.Id == s.FarmerId).Select(f => f.FieldOfficerUserId).SingleAsync())).ShouldBe(s.PplB);

        string form = await client.GetStringAsync(new Uri($"/Partnership/Farmers/Edit/{s.FarmerId}", UriKind.Relative));
        form.ShouldContain("PPL B");

        string lookup = await client.GetStringAsync(new Uri($"/Lookup/FieldOfficers?branchId={s.BranchId}", UriKind.Relative));
        lookup.ShouldContain(s.PplA.ToString());
        lookup.ShouldContain(s.PplB.ToString());
    }

    private sealed record Seed(Guid BranchId, Guid PplA, Guid PplB, Guid FarmerId, string FarmerCode, Guid CoopId, string CoopCode);

    private async Task<Seed> SeedAsync()
    {
        string s = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var branch = Branch.Create($"F{s}", $"Cabang {s}", null, null);
        BranchAccessProfile profile = BranchAccessProfile.Create($"PPL area {s}", null, false, [branch.Id]).Value;
        db.Branches.Add(branch);
        db.BranchAccessProfiles.Add(profile);
        await db.SaveChangesAsync();

        Guid pplRole = await db.Roles.Where(r => r.Name == MobileRoles.FieldOfficer).Select(r => r.Id).SingleAsync();
        Guid pplA = await CreatePplAsync(db, $"ppla-{s}", "PPL A", profile.Id, branch.Id, pplRole);
        Guid pplB = await CreatePplAsync(db, $"pplb-{s}", "PPL B", profile.Id, branch.Id, pplRole);

        Farmer farmer = Farmer.Create($"FI{s}", "Peternak Inti", FarmerType.Inti, branch.Id, null,
            TaxIdentity.None, null, null, BankAccount.None).Value;
        Coop coop = Coop.Create(farmer, $"KI{s}", "Kandang Inti", 5_000, HouseType.ClosedHouse, null, null, null).Value;
        db.Farmers.Add(farmer);
        db.Coops.Add(coop);
        await db.SaveChangesAsync();

        return new Seed(branch.Id, pplA, pplB, farmer.Id, farmer.Code, coop.Id, coop.Code);
    }

    private static async Task<Guid> CreatePplAsync(
        ApplicationDbContext db, string email, string firstName, Guid profileId, Guid branchId, Guid roleId)
    {
        var user = User.Create($"{email}@intiplasma.test", firstName, "Lapangan", "hash");
        user.SetAccess(null, profileId, branchId);
        user.SetRoles([roleId]);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }

    private async Task<T> QueryAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, List<KeyValuePair<string, string>> fields)
    {
        string page = await client.GetStringAsync(new Uri("/Finance/CostCenters/Create", UriKind.Relative));
        fields.Add(new("__RequestVerificationToken", AntiforgeryToken(page)));

        using var content = new FormUrlEncodedContent(fields);
        return await client.PostAsync(new Uri(path, UriKind.Relative), content);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        string page = await client.GetStringAsync(new Uri("/Auth/Login", UriKind.Relative));

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = WebAppFactory.AdminEmail,
            ["Password"] = WebAppFactory.AdminPassword,
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        });
        (await client.PostAsync(new Uri("/Auth/Login", UriKind.Relative), form)).StatusCode.ShouldBe(HttpStatusCode.Redirect);

        return client;
    }

    private static string AntiforgeryToken(string html) => TokenRegex().Match(html).Groups[1].Value;

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
