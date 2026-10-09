using System.Net.Http.Json;
using Domain.Inventory.Stock;
using Domain.MasterData.Coops;
using Domain.MasterData.Farmers;
using Domain.MasterData.Items;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Cycles;
using Domain.Roles;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Production;

/// <summary>
/// Field data scenario shared by the mobile tests (PLAN-MOBILE M2, M3): a branch with two PPL and a Manager, and a
/// running cycle with feed in stock in each PPL's coop.
/// </summary>
public abstract class FieldScenarioTest(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    protected static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record RoleResponse(Guid Id, string Name);

    protected sealed record Setup(
        Guid Branch,
        string PplA,
        string PplB,
        string Manager,
        Guid CycleA,
        Guid CycleB,
        Guid WarehouseA,
        Guid WarehouseB,
        Guid Central,
        Guid Feed,
        Guid Sak);

    /// <summary>
    /// A branch with PPL A (coop A), PPL B (coop B) and a Manager; both coops run a cycle of 1,000 birds that
    /// started three days ago and hold 1,000 kg of feed; the branch also has a central warehouse with feed.
    /// </summary>
    protected async Task<Setup> SetupAsync()
    {
        await AuthenticateAsAdminAsync();
        string suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        Guid branch = await PostAsync<Guid>("branches", new { code = $"R{suffix}", name = "Cabang Recording" });
        Guid profile = await PostAsync<Guid>("branch-access-profiles",
            new { name = $"Rec {suffix}", allBranches = false, branchIds = new[] { branch } });
        List<RoleResponse>? roles = await HttpClient.GetFromJsonAsync<List<RoleResponse>>("roles");
        Guid pplRole = roles!.Single(r => r.Name == MobileRoles.FieldOfficer).Id;
        Guid managerRole = roles!.Single(r => r.Name == MobileRoles.Manager).Id;

        (Guid pplAId, string pplA) = await CreateUserAsync(pplRole, profile, branch);
        (Guid pplBId, string pplB) = await CreateUserAsync(pplRole, profile, branch);
        (_, string manager) = await CreateUserAsync(managerRole, profile, branch);

        await AuthenticateAsAdminAsync();
        Guid farmer = await PostAsync<Guid>("farmers", new
        {
            code = $"RF{suffix}",
            name = "Peternak Recording",
            type = "Inti",
            branchId = branch,
            taxIdentity = new { isPkp = false },
            bankAccount = new { },
            fieldOfficerUserId = (Guid?)null
        });
        Guid coopA = await PostAsync<Guid>("coops", Coop(farmer, $"RA{suffix}", pplAId));
        Guid coopB = await PostAsync<Guid>("coops", Coop(farmer, $"RB{suffix}", pplBId));

        Guid warehouseA = await CoopWarehouseAsync(coopA);
        Guid warehouseB = await CoopWarehouseAsync(coopB);

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Guid kg = await db.Uoms.Where(u => u.Code == "KG").Select(u => u.Id).SingleAsync();
        Guid sak = await db.Uoms.Where(u => u.Code == "SAK").Select(u => u.Id).SingleAsync();
        var feed = Item.Create($"RP{suffix}", "Pakan Starter", ItemCategory.Feed, kg, null);
        feed.SetConversions([(sak, 50m)]).IsSuccess.ShouldBeTrue();
        db.Items.Add(feed);

        var central = Warehouse.CreateCentral($"RG{suffix}", "Gudang Induk", branch, null);
        db.Warehouses.Add(central);

        foreach (Guid warehouseId in new[] { warehouseA, warehouseB, central.Id })
        {
            var balance = StockBalance.Open(warehouseId, feed.Id);
            var movement = new StockMovement(Today, StockMovementType.Receipt, "Test", Guid.NewGuid(), $"TEST-{suffix}", null);
            db.StockLedgerEntries.Add(balance.Receive(movement, 1_000m, new Money(8_000_000m)).Value);
            db.StockBalances.Add(balance);
        }

        Farmer farmerEntity = await db.Farmers.SingleAsync(f => f.Id == farmer);
        Guid cycleA = StartCycle(db, await db.Coops.SingleAsync(c => c.Id == coopA), farmerEntity, $"RCA{suffix}");
        Guid cycleB = StartCycle(db, await db.Coops.SingleAsync(c => c.Id == coopB), farmerEntity, $"RCB{suffix}");

        await db.SaveChangesAsync();

        return new Setup(branch, pplA, pplB, manager, cycleA, cycleB, warehouseA, warehouseB, central.Id, feed.Id, sak);
    }

    protected static Guid StartCycle(ApplicationDbContext db, Coop coop, Farmer farmer, string number)
    {
        ProductionCycle cycle = ProductionCycle.Plan(number, coop, farmer, null, Today.AddDays(-3), 1_000, null).Value;
        cycle.Start(Today.AddDays(-3), 1_000).IsSuccess.ShouldBeTrue();
        db.ProductionCycles.Add(cycle);

        return cycle.Id;
    }

    /// <summary>
    /// The coop warehouse is created by the outbox after the coop is saved.
    /// </summary>
    protected async Task<Guid> CoopWarehouseAsync(Guid coopId)
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            Guid? id = await CountAsync(db => db.Warehouses.Where(w => w.CoopId == coopId).Select(w => (Guid?)w.Id).SingleOrDefaultAsync());
            if (id is { } warehouseId)
            {
                return warehouseId;
            }

            await Task.Delay(500);
        }

        throw new InvalidOperationException($"No warehouse was created for coop {coopId}");
    }

    protected async Task<T> CountAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using IServiceScope scope = Services.CreateScope();

        return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    protected static object Coop(Guid farmerId, string code, Guid fieldOfficerUserId) => new
    {
        farmerId,
        code,
        name = $"Kandang {code}",
        capacity = 5_000,
        houseType = "ClosedHouse",
        fieldOfficerUserId
    };

    protected async Task<(Guid Id, string AccessToken)> CreateUserAsync(Guid roleId, Guid profileId, Guid branchId)
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);

        await AuthenticateAsAdminAsync();
        (await HttpClient.PutAsJsonAsync($"users/{userId}/roles", new { roleIds = new[] { roleId } })).EnsureSuccessStatusCode();
        (await HttpClient.PutAsJsonAsync($"users/{userId}/access", new { branchAccessProfileId = profileId, defaultBranchId = branchId }))
            .EnsureSuccessStatusCode();

        return (userId, (await LoginAsync(email)).AccessToken);
    }

#pragma warning disable CA1054 // Relative API paths, as everywhere in these tests.
    protected async Task<T> PostAsync<T>(string url, object body)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(url, body);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"POST {url} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
#pragma warning restore CA1054
}
