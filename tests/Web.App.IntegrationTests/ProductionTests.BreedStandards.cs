using System.Net;
using Domain.MasterData.BreedStandards;
using Microsoft.EntityFrameworkCore;

namespace Web.App.IntegrationTests;

public sealed partial class ProductionTests
{
    /// <summary>
    /// PLAN-MOBILE M5: Master Data → Breed Standards (create from CSV, import, export, default) and the standard of a
    /// cycle (plan, change, Performance tab).
    /// </summary>
    [Fact]
    public async Task BreedStandards_Should_ImportRows_AndApplyToACycle()
    {
        HttpClient client = await AdminClientAsync();
        string s = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

        (await client.GetAsync(new Uri("/MasterData/BreedStandards/Create", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Created from pasted CSV; a decreasing body weight is refused.
        HttpResponseMessage invalid = await PostAsync(client, "/MasterData/BreedStandards/Create", new()
        {
            ["Code"] = $"X{s}", ["Name"] = "Invalid", ["RowsCsv"] = "age,bw\n0,42\n7,30\n"
        });
        invalid.StatusCode.ShouldBe(HttpStatusCode.OK);
        ErrorOf(await invalid.Content.ReadAsStringAsync()).ShouldContain("body weight");

        HttpResponseMessage created = await PostAsync(client, "/MasterData/BreedStandards/Create", new()
        {
            ["Code"] = $"S{s}", ["Name"] = $"Standard {s}", ["Strain"] = "CP 707", ["RowsCsv"] = "age,bw,fcr\n0,10,\n70,700,2.5\n"
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Redirect, ErrorOf(await created.Content.ReadAsStringAsync()));
        Guid standardId = await QueryAsync(db => db.BreedStandards.Where(b => b.Code == $"S{s}").Select(b => b.Id).SingleAsync());
        (await client.GetStringAsync(new Uri("/MasterData/BreedStandards", UriKind.Relative))).ShouldContain($"S{s}");

        // Import replaces the rows (';' with decimal comma); an invalid file leaves them as they are.
        (await PostAsync(client, $"/MasterData/BreedStandards/Import?id={standardId}", new()
        {
            ["csv"] = "age;bw;fcr;depletion\n0;10;;0\n35;400,5;1,6;3\n70;700;2,5;6\n"
        })).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await PostAsync(client, $"/MasterData/BreedStandards/Import?id={standardId}", new() { ["csv"] = "bw\n10\n" }))
            .StatusCode.ShouldBe(HttpStatusCode.Redirect);
        List<decimal> weights = await QueryAsync(db => db.BreedStandards.Where(b => b.Id == standardId)
            .SelectMany(b => b.Rows).OrderBy(r => r.AgeDays).Select(r => r.BodyWeightGram).ToListAsync());
        weights.ShouldBe([10m, 400.5m, 700m]);

        HttpResponseMessage export = await client.GetAsync(new Uri($"/MasterData/BreedStandards/ExportRows?id={standardId}", UriKind.Relative));
        export.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await export.Content.ReadAsStringAsync()).ShouldStartWith("age,bw,fcr,depletion,daily_feed,cum_feed");

        // Edit from the table; then make it the default.
        HttpResponseMessage edited = await PostAsync(client, "/MasterData/BreedStandards/Edit", new()
        {
            ["Id"] = standardId.ToString(), ["Code"] = $"S{s}", ["Name"] = $"Standard {s}", ["IsActive"] = "true",
            ["Rows[0].AgeDays"] = "0", ["Rows[0].BodyWeightGram"] = "10",
            ["Rows[1].AgeDays"] = "70", ["Rows[1].BodyWeightGram"] = "700", ["Rows[1].Fcr"] = "2.5"
        });
        edited.StatusCode.ShouldBe(HttpStatusCode.Redirect, ErrorOf(await edited.Content.ReadAsStringAsync()));
        (await PostAsync(client, $"/MasterData/BreedStandards/SetDefault?id={standardId}", [])).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await QueryAsync(db => db.BreedStandards.Where(b => b.IsDefault).Select(b => b.Id).ToListAsync())).ShouldBe([standardId]);

        // The default cannot be deactivated.
        HttpResponseMessage deactivate = await PostAsync(client, "/MasterData/BreedStandards/Edit", new()
        {
            ["Id"] = standardId.ToString(), ["Code"] = $"S{s}", ["Name"] = $"Standard {s}",
            ["Rows[0].AgeDays"] = "0", ["Rows[0].BodyWeightGram"] = "10"
        });
        deactivate.StatusCode.ShouldBe(HttpStatusCode.OK);
        ErrorOf(await deactivate.Content.ReadAsStringAsync()).ShouldContain("default");

        // A cycle planned with the standard, then switched to the default.
        (await client.GetStringAsync(new Uri("/Production/Cycles/Create", UriKind.Relative))).ShouldContain($"Standard {s}");
        await PostAsync(client, "/Admin/Branches/Create", new() { ["Code"] = $"B{s}", ["Name"] = $"Branch {s}" });
        Guid branchId = await QueryAsync(db => db.Branches.Where(b => b.Code == $"B{s}").Select(b => b.Id).SingleAsync());
        await PostAsync(client, "/Partnership/Farmers/Create", new() { ["Code"] = $"F{s}", ["Name"] = "Inti farm", ["Type"] = "Inti", ["BranchId"] = branchId.ToString() });
        Guid farmerId = await QueryAsync(db => db.Farmers.Where(f => f.Code == $"F{s}").Select(f => f.Id).SingleAsync());
        Guid coopId = await CreateCoopAsync(client, s, "K", farmerId, branchId);

        Guid other = await QueryAsync(async db =>
        {
            BreedStandard standard = BreedStandard.Create($"O{s}", $"Other {s}", null, null, [new StandardPoint(0, 40m, null, null, null, null)]).Value;
            db.BreedStandards.Add(standard);
            await db.SaveChangesAsync();
            return standard.Id;
        });

        HttpResponseMessage planned = await PostAsync(client, "/Production/Cycles/Create", new()
        {
            ["CoopId"] = coopId.ToString(), ["PlannedChickInDate"] = Today(), ["PlannedPopulation"] = "1000", ["BreedStandardId"] = other.ToString()
        });
        planned.StatusCode.ShouldBe(HttpStatusCode.Redirect, ErrorOf(await planned.Content.ReadAsStringAsync()));
        Guid cycleId = await QueryAsync(db => db.ProductionCycles.Where(c => c.CoopId == coopId).Select(c => c.Id).SingleAsync());
        (await CycleAsync(cycleId)).BreedStandardId.ShouldBe(other);
        (await client.GetStringAsync(new Uri($"/Production/Cycles/Details/{cycleId}", UriKind.Relative))).ShouldContain($"Other {s}");

        (await PostAsync(client, $"/Production/Cycles/BreedStandard?id={cycleId}", new() { ["breedStandardId"] = string.Empty }))
            .StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await CycleAsync(cycleId)).BreedStandardId.ShouldBeNull();

        string details = await client.GetStringAsync(new Uri($"/Production/Cycles/Details/{cycleId}?tab=performance", UriKind.Relative));
        details.ShouldContain($"Standard {s} (default)");
        details.ShouldContain("Change standard");
    }
}
