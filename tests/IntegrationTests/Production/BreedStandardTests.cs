using System.Net;
using System.Net.Http.Json;
using Domain.MasterData.BreedStandards;
using Domain.Partnership.Cycles;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Production;

/// <summary>
/// PLAN-MOBILE M5: the cycle performance and the mobile dashboard against the breed standard (M-71, M-74, M-77).
/// Standards are global, so the default one used here is lenient enough not to flag the other tests' cycles.
/// </summary>
public sealed class BreedStandardTests(IntegrationTestWebAppFactory factory) : FieldScenarioTest(factory)
{
    private sealed record Performance(Guid CycleId, PerformanceStandard? Standard, StandardStatus? StandardStatus, List<Day> Days);

    private sealed record PerformanceStandard(Guid Id, string Name, bool IsDefault, bool IsFrozen, List<StandardPoint> Rows);

    private sealed record StandardStatus(int? WeighingAgeDays, decimal? StandardBodyWeightGram, decimal? BodyWeightPercent, bool BodyWeightBelow, bool BelowStandard);

    private sealed record Day(int AgeDays, StandardValue? Standard, StandardDeviation? Deviation);

    private sealed record Dashboard(Kpi Kpi, List<DashboardCycle> Cycles);

    private sealed record Kpi(int CyclesBelowStandard);

    private sealed record DashboardCycle(Guid CycleId, string? StandardName, List<string> Flags);

    private sealed record StandardListItem(Guid Id, bool IsActive);

    [Fact]
    public async Task Performance_Should_UseTheDefault_ThenTheCyclesOwnStandard()
    {
        Setup s = await SetupAsync();
        string suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        // Lenient default: 10 g at day 0 up to 700 g at day 70, FCR 2,5 and 10% depletion.
        Guid lenient = await AddStandardAsync($"L{suffix}", makeDefault: true,
            new StandardPoint(0, 10m, null, 0m, null, 0m), new StandardPoint(70, 700m, 2.5m, 10m, null, 8_000m));
        // Strict: 105 g expected at day 3.
        Guid strict = await AddStandardAsync($"S{suffix}", makeDefault: false,
            new StandardPoint(0, 42m, null, 0m, null, 0m), new StandardPoint(7, 190m, 0.8m, 0.1m, null, 160m));

        Authenticate(s.PplA);
        await RecordAsync(s, bodyWeightGram: 60m);

        Performance byDefault = await PerformanceAsync(s.CycleA);
        byDefault.Standard!.Id.ShouldBe(lenient);
        byDefault.Standard.IsDefault.ShouldBeTrue();
        byDefault.Standard.Rows.Count.ShouldBe(2);
        byDefault.Days.ShouldHaveSingleItem().Standard!.BodyWeightGram.ShouldBe(39.571m); // 10 + 690 × 3/70
        byDefault.StandardStatus!.BelowStandard.ShouldBeFalse();

        // A PPL cannot change the standard; an inactive or unknown standard is refused.
        (await HttpClient.PutAsJsonAsync($"cycles/{s.CycleA}/breed-standard", new { breedStandardId = strict }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await AuthenticateAsAdminAsync();
        (await HttpClient.PutAsJsonAsync($"cycles/{s.CycleA}/breed-standard", new { breedStandardId = Guid.NewGuid() }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await HttpClient.PutAsJsonAsync($"cycles/{s.CycleA}/breed-standard", new { breedStandardId = strict }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Performance own = await PerformanceAsync(s.CycleA);
        own.Standard!.Id.ShouldBe(strict);
        own.Standard.IsDefault.ShouldBeFalse();
        own.StandardStatus!.WeighingAgeDays.ShouldBe(3);
        own.StandardStatus.StandardBodyWeightGram.ShouldBe(105.429m);
        own.StandardStatus.BodyWeightBelow.ShouldBeTrue();
        own.Days[0].Deviation!.BodyWeightPercent.ShouldBe(-43.09m);

        // The dashboard flags the cycle, for the PPL and in the Manager's KPI.
        Authenticate(s.PplA);
        DashboardCycle cycle = (await DashboardAsync()).Cycles.ShouldHaveSingleItem();
        cycle.StandardName.ShouldBe($"Standard S{suffix}");
        cycle.Flags.ShouldContain("BelowStandard");

        Authenticate(s.Manager);
        Dashboard manager = await DashboardAsync();
        manager.Kpi.CyclesBelowStandard.ShouldBe(1);
        manager.Cycles.Single(c => c.CycleId == s.CycleB).Flags.ShouldNotContain("BelowStandard");

        // Only active standards are listed by default.
        await UpdateStandardAsync(strict, s => s.Update(s.Name, s.Strain, s.Notes, isActive: false));
        List<StandardListItem>? active = await HttpClient.GetFromJsonAsync<List<StandardListItem>>("breed-standards");
        active!.Select(a => a.Id).ShouldNotContain(strict);
        List<StandardListItem>? all = await HttpClient.GetFromJsonAsync<List<StandardListItem>>("breed-standards?activeOnly=false");
        all!.ShouldContain(a => a.Id == strict && !a.IsActive);

        await AuthenticateAsAdminAsync();
        HttpResponseMessage inactive = await HttpClient.PutAsJsonAsync($"cycles/{s.CycleB}/breed-standard", new { breedStandardId = strict });
        inactive.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await inactive.Content.ReadAsStringAsync()).ShouldContain("BreedStandards.Inactive");
    }

    [Fact]
    public async Task ClosedCycle_Should_KeepItsFrozenStandard()
    {
        Setup s = await SetupAsync();
        string suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        Guid standardId = await AddStandardAsync($"F{suffix}", makeDefault: false,
            new StandardPoint(0, 40m, null, 0m, null, 0m), new StandardPoint(70, 3_000m, 2.5m, 10m, null, 8_000m));

        // Close cycle B with the standard frozen, as CloseCycleCommand does once harvest, stock and sales are done.
        using (IServiceScope scope = Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            BreedStandard standard = await db.BreedStandards.Include(b => b.Rows).SingleAsync(b => b.Id == standardId);
            ProductionCycle cycle = await db.ProductionCycles.Include(c => c.Harvests).SingleAsync(c => c.Id == s.CycleB);
            cycle.SetBreedStandard(standardId, null).IsSuccess.ShouldBeTrue();
            cycle.RecordHarvest(Today, 1_000, 1_800m, null).IsSuccess.ShouldBeTrue();
            cycle.Close(
                CyclePerformance.Calculate(1_000, 0, 0, 1_000, 1_800m, 3_000m, 1.8m, 3m),
                CycleCostSummary.Calculate(1m, 1m, 0m, 1_000, 1_800m, 2m),
                standard.ToSnapshot()).IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync();
        }

        // The master changes afterwards: other rows and a new name.
        await UpdateStandardAsync(standardId, s =>
        {
            s.Update("Renamed", s.Strain, s.Notes, isActive: true);
            s.ReplaceRows([new StandardPoint(0, 50m, null, 0m, null, 0m)]);
        });

        Authenticate(s.Manager);
        Performance closed = await PerformanceAsync(s.CycleB);
        closed.Standard!.Id.ShouldBe(standardId);
        closed.Standard.IsFrozen.ShouldBeTrue();
        closed.Standard.Name.ShouldBe($"Standard F{suffix}");
        closed.Standard.Rows.Select(r => r.BodyWeightGram).ShouldBe([40m, 3_000m]);
    }

    private async Task<Guid> AddStandardAsync(string code, bool makeDefault, params StandardPoint[] rows)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        BreedStandard standard = BreedStandard.Create(code, $"Standard {code}", "Test", null, rows).Value;
        if (makeDefault)
        {
            foreach (BreedStandard other in await db.BreedStandards.Where(b => b.IsDefault).ToListAsync())
            {
                other.ClearDefault();
            }

            standard.MakeDefault().IsSuccess.ShouldBeTrue();
        }

        db.BreedStandards.Add(standard);
        await db.SaveChangesAsync();

        return standard.Id;
    }

    private async Task UpdateStandardAsync(Guid id, Action<BreedStandard> change)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        BreedStandard standard = await db.BreedStandards.Include(b => b.Rows).SingleAsync(b => b.Id == id);
        change(standard);
        await db.SaveChangesAsync();
    }

    private async Task RecordAsync(Setup s, decimal bodyWeightGram)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "production/daily-recordings")
        {
            Content = JsonContent.Create(new
            {
                id = Guid.CreateVersion7(),
                cycleId = s.CycleA,
                date = Today,
                mortality = 5,
                culling = 0,
                averageBodyWeightGram = bodyWeightGram,
                usages = new[] { new { itemId = s.Feed, uomId = s.Sak, quantity = 1m } },
                documents = Array.Empty<Guid>()
            })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        HttpResponseMessage response = await HttpClient.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task<Performance> PerformanceAsync(Guid cycleId)
    {
        HttpResponseMessage response = await HttpClient.GetAsync($"cycles/{cycleId}/performance");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<Performance>())!;
    }

    private async Task<Dashboard> DashboardAsync()
    {
        HttpResponseMessage response = await HttpClient.GetAsync($"mobile/dashboard?date={Today:yyyy-MM-dd}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<Dashboard>())!;
    }
}
