using System.Net;
using System.Text.RegularExpressions;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Cycles;
using Infrastructure.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.App.Infrastructure.Export;

namespace Web.App.IntegrationTests;

/// <summary>
/// PhaseW5: plan → DOC in the coop warehouse → chick-in → daily recording (+ revision) → harvest → close rules,
/// cycle summary PDF and exports.
/// </summary>
[Collection(nameof(WebAppCollection))]
public sealed partial class ProductionTests(WebAppFactory factory)
{
    public static TheoryData<string> Pages =>
    [
        "/Production/Cycles", "/Production/Cycles/Create", "/Production/Recordings", "/Production/Harvests",
        "/Production/LiveBirdStock", "/MasterData/WeightRanges", "/MasterData/WeightRanges/Create"
    ];

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Admin_Should_OpenPage(string path)
    {
        HttpClient client = await AdminClientAsync();

        (await client.GetAsync(new Uri(path, UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cycle_Should_GoFromPlan_ThroughChickIn_RecordingAndHarvest()
    {
        HttpClient client = await AdminClientAsync();
        string s = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

        // Master data and a coop (its warehouse is normally created by the outbox, which is off in tests)
        await PostAsync(client, "/Admin/Branches/Create", new() { ["Code"] = $"B{s}", ["Name"] = $"Branch {s}" });
        Guid branchId = await QueryAsync(db => db.Branches.Where(b => b.Code == $"B{s}").Select(b => b.Id).SingleAsync());
        Guid ekor = await QueryAsync(db => db.Uoms.Where(u => u.Code == "EKOR").Select(u => u.Id).SingleAsync());
        Guid kg = await QueryAsync(db => db.Uoms.Where(u => u.Code == "KG").Select(u => u.Id).SingleAsync());
        await PostAsync(client, "/MasterData/Items/Create", new() { ["Code"] = $"D{s}", ["Name"] = "DOC", ["Category"] = "Doc", ["BaseUomId"] = ekor.ToString() });
        await PostAsync(client, "/MasterData/Items/Create", new() { ["Code"] = $"P{s}", ["Name"] = "Feed", ["Category"] = "Feed", ["BaseUomId"] = kg.ToString() });
        Guid docId = await QueryAsync(db => db.Items.Where(i => i.Code == $"D{s}").Select(i => i.Id).SingleAsync());
        Guid feedId = await QueryAsync(db => db.Items.Where(i => i.Code == $"P{s}").Select(i => i.Id).SingleAsync());
        await PostAsync(client, "/MasterData/Vendors/Create", new() { ["Code"] = $"V{s}", ["Name"] = "Vendor", ["PaymentTermDays"] = "30" });
        Guid vendorId = await QueryAsync(db => db.Vendors.Where(v => v.Code == $"V{s}").Select(v => v.Id).SingleAsync());
        await PostAsync(client, "/Partnership/Farmers/Create", new() { ["Code"] = $"F{s}", ["Name"] = "Inti farm", ["Type"] = "Inti", ["BranchId"] = branchId.ToString() });
        Guid farmerId = await QueryAsync(db => db.Farmers.Where(f => f.Code == $"F{s}").Select(f => f.Id).SingleAsync());
        Guid coopId = await CreateCoopAsync(client, s, "K", farmerId, branchId);
        Guid otherCoopId = await CreateCoopAsync(client, s, "L", farmerId, branchId);
        Guid warehouseId = await QueryAsync(db => db.Warehouses.Where(w => w.CoopId == coopId).Select(w => w.Id).SingleAsync());

        // Plan: the coop leaves the plannable list
        HttpResponseMessage planned = await PostAsync(client, "/Production/Cycles/Create", new()
        {
            ["CoopId"] = coopId.ToString(), ["PlannedChickInDate"] = Today(), ["PlannedPopulation"] = "1000"
        });
        planned.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var cycleId = Guid.Parse(planned.Headers.Location!.ToString().Split('/')[^1]);
        (await client.GetStringAsync(new Uri($"/Lookup/PlannableCoops?q=K{s}", UriKind.Relative))).ShouldNotContain($"K{s}");
        (await client.GetStringAsync(new Uri($"/Production/Cycles/Details/{cycleId}", UriKind.Relative))).ShouldContain("Transfer DOC");

        // DOC received straight into the coop warehouse (charged to the planned cycle); feed goes to a central
        // warehouse and is transferred to the coop
        await PostAsync(client, "/MasterData/Warehouses/Create", new() { ["Code"] = $"GP{s}", ["Name"] = "Gudang Pusat", ["BranchId"] = branchId.ToString() });
        Guid centralId = await QueryAsync(db => db.Warehouses.Where(w => w.Code == $"GP{s}").Select(w => w.Id).SingleAsync());
        HttpResponseMessage order = await PostAsync(client, "/Procurement/PurchaseOrders/Create", new()
        {
            ["BranchId"] = branchId.ToString(), ["VendorId"] = vendorId.ToString(), ["OrderDate"] = Today(),
            ["Lines[0].ItemId"] = docId.ToString(), ["Lines[0].UomId"] = ekor.ToString(), ["Lines[0].Quantity"] = "1000", ["Lines[0].UnitPrice"] = "7000",
            ["Lines[1].ItemId"] = feedId.ToString(), ["Lines[1].UomId"] = kg.ToString(), ["Lines[1].Quantity"] = "500", ["Lines[1].UnitPrice"] = "8000"
        });
        var orderId = Guid.Parse(order.Headers.Location!.ToString().Split('/')[^1]);
        await PostAsync(client, $"/Procurement/PurchaseOrders/Approve/{orderId}", []);
        HttpResponseMessage receipt = await PostAsync(client, "/Inventory/GoodsReceipts/Create", new()
        {
            ["PurchaseOrderId"] = orderId.ToString(), ["WarehouseId"] = warehouseId.ToString(), ["ReceiptDate"] = Today(),
            ["Lines[0].PurchaseOrderLineNumber"] = "1", ["Lines[0].Quantity"] = "1000"
        });
        receipt.StatusCode.ShouldBe(HttpStatusCode.Redirect, ErrorOf(await receipt.Content.ReadAsStringAsync()));
        HttpResponseMessage feedReceipt = await PostAsync(client, "/Inventory/GoodsReceipts/Create", new()
        {
            ["PurchaseOrderId"] = orderId.ToString(), ["WarehouseId"] = centralId.ToString(), ["ReceiptDate"] = Today(),
            ["Lines[0].PurchaseOrderLineNumber"] = "2", ["Lines[0].Quantity"] = "500"
        });
        feedReceipt.StatusCode.ShouldBe(HttpStatusCode.Redirect, ErrorOf(await feedReceipt.Content.ReadAsStringAsync()));
        HttpResponseMessage transfer = await PostAsync(client, "/Inventory/StockTransfers/Create", new()
        {
            ["FromWarehouseId"] = centralId.ToString(), ["ToWarehouseId"] = warehouseId.ToString(), ["Date"] = Today(),
            ["Lines[0].ItemId"] = feedId.ToString(), ["Lines[0].UomId"] = kg.ToString(), ["Lines[0].Quantity"] = "300"
        });
        transfer.StatusCode.ShouldBe(HttpStatusCode.Redirect, ErrorOf(await transfer.Content.ReadAsStringAsync()));

        string chickInForm = await client.GetStringAsync(new Uri($"/Production/Cycles/ChickIn/{cycleId}", UriKind.Relative));
        chickInForm.ShouldContain($"D{s}");

        // Chick-in 1000 birds → active
        (await PostAsync(client, $"/Production/Cycles/ChickIn/{cycleId}", new()
        {
            ["ChickInDate"] = Today(), ["Lines[0].ItemId"] = docId.ToString(), ["Lines[0].Quantity"] = "1000"
        })).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await CycleAsync(cycleId)).Status.ShouldBe(CycleStatus.Active);
        (await CycleAsync(cycleId)).InitialPopulation.ShouldBe(1000);

        // Daily recording with feed usage, then a revision
        (await PostAsync(client, "/Production/Recordings/Create", new()
        {
            ["CycleId"] = cycleId.ToString(), ["Date"] = Today(), ["Mortality"] = "5", ["Culling"] = "1", ["AverageBodyWeightGram"] = "45",
            ["Usages[0].ItemId"] = feedId.ToString(), ["Usages[0].UomId"] = kg.ToString(), ["Usages[0].Quantity"] = "20"
        })).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        Guid recordingId = await QueryAsync(db => db.DailyRecordings.Where(r => r.CycleId == cycleId).Select(r => r.Id).SingleAsync());
        (await client.GetStringAsync(new Uri($"/Production/Recordings?cycleId={cycleId}", UriKind.Relative))).ShouldContain("20,0");

        (await PostAsync(client, "/Production/Recordings/Revise", new()
        {
            ["Id"] = recordingId.ToString(), ["CycleId"] = cycleId.ToString(), ["Date"] = Today(), ["Reason"] = "Recount",
            ["Mortality"] = "7", ["Culling"] = "1", ["AverageBodyWeightGram"] = "46",
            ["Usages[0].ItemId"] = feedId.ToString(), ["Usages[0].UomId"] = kg.ToString(), ["Usages[0].Quantity"] = "25"
        })).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        string recording = await client.GetStringAsync(new Uri($"/Production/Recordings/Details/{recordingId}", UriKind.Relative));
        recording.ShouldContain("Revision 1");
        recording.ShouldContain("Recount");
        (await CycleAsync(cycleId)).TotalMortality.ShouldBe(7);

        // Harvest → harvesting; closing is refused while birds remain
        (await PostAsync(client, "/Production/Harvests/Create", new()
        {
            ["Harvest.CycleId"] = cycleId.ToString(), ["Harvest.Date"] = Today(), ["Harvest.Birds"] = "400", ["Harvest.WeightKg"] = "820.5",
            ["Harvest.Notes"] = "Truck B 1234 XY"
        })).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await CycleAsync(cycleId)).Status.ShouldBe(CycleStatus.Harvesting);
        (await client.GetStringAsync(new Uri($"/Production/Harvests?cycleId={cycleId}", UriKind.Relative))).ShouldContain("Truck B 1234 XY");

        await PostAsync(client, $"/Production/Cycles/Close/{cycleId}", []);
        (await CycleAsync(cycleId)).Status.ShouldBe(CycleStatus.Harvesting);

        string details = await client.GetStringAsync(new Uri($"/Production/Cycles/Details/{cycleId}?tab=performance", UriKind.Relative));
        details.ShouldContain("chart-population");
        details.ShouldContain("Running cost");

        HttpResponseMessage pdf = await client.GetAsync(new Uri($"/Production/Cycles/Print/{cycleId}", UriKind.Relative));
        System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync(), 0, 5).ShouldBe("%PDF-");
        HttpResponseMessage export = await client.GetAsync(new Uri($"/Production/Recordings/Export?format=xlsx&cycleId={cycleId}", UriKind.Relative));
        export.Content.Headers.ContentType!.MediaType.ShouldBe(ExcelExporter.ContentType);

        // A planned cycle can be cancelled (with a reason)
        HttpResponseMessage second = await PostAsync(client, "/Production/Cycles/Create", new()
        {
            ["CoopId"] = otherCoopId.ToString(), ["PlannedChickInDate"] = Today(), ["PlannedPopulation"] = "500"
        });
        var secondId = Guid.Parse(second.Headers.Location!.ToString().Split('/')[^1]);
        await PostAsync(client, $"/Production/Cycles/Cancel/{secondId}", new() { ["reason"] = "Coop under repair" });
        (await CycleAsync(secondId)).Status.ShouldBe(CycleStatus.Cancelled);
    }

    /// <summary>
    /// PLAN-MOBILE M3: weight ranges (no overlap among active ranges) and the live bird stock summary with export.
    /// </summary>
    [Fact]
    public async Task WeightRanges_Should_RejectOverlaps_AndLiveBirdStock_ShouldExport()
    {
        HttpClient client = await AdminClientAsync();
        string s = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();

        // Far above real broiler weights, so the range never meets the ranges of other tests.
        HttpResponseMessage created = await PostAsync(client, "/MasterData/WeightRanges/Create", new()
        {
            ["Code"] = $"W{s}", ["Name"] = "Test range", ["MinWeightKg"] = "500", ["MaxWeightKg"] = "501", ["SortOrder"] = "90"
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Redirect, ErrorOf(await created.Content.ReadAsStringAsync()));
        (await client.GetStringAsync(new Uri("/MasterData/WeightRanges", UriKind.Relative))).ShouldContain($"W{s}");

        HttpResponseMessage overlapping = await PostAsync(client, "/MasterData/WeightRanges/Create", new()
        {
            ["Code"] = $"X{s}", ["Name"] = "Overlap", ["MinWeightKg"] = "500.5", ["MaxWeightKg"] = "502", ["SortOrder"] = "91"
        });
        overlapping.StatusCode.ShouldBe(HttpStatusCode.OK);
        ErrorOf(await overlapping.Content.ReadAsStringAsync()).ShouldContain($"W{s}");

        (await client.GetStringAsync(new Uri($"/Production/LiveBirdStock?date={Today()}", UriKind.Relative))).ShouldContain("Per weight range");
        HttpResponseMessage export = await client.GetAsync(new Uri($"/Production/LiveBirdStock/Export?format=xlsx&date={Today()}", UriKind.Relative));
        export.StatusCode.ShouldBe(HttpStatusCode.OK);
        export.Content.Headers.ContentDisposition!.FileName!.ShouldContain("live-bird-stock-");
    }

    private async Task<Guid> CreateCoopAsync(HttpClient client, string s, string prefix, Guid farmerId, Guid branchId)
    {
        await PostAsync(client, "/Partnership/Coops/Create", new()
        {
            ["FarmerId"] = farmerId.ToString(), ["Code"] = $"{prefix}{s}", ["Name"] = $"Coop {prefix}", ["Capacity"] = "5000", ["HouseType"] = "ClosedHouse"
        });
        Guid coopId = await QueryAsync(db => db.Coops.Where(c => c.Code == $"{prefix}{s}").Select(c => c.Id).SingleAsync());

        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Warehouses.Add(Warehouse.CreateForCoop($"GK-{prefix}{s}", $"Gudang Kandang {prefix}", branchId, coopId, null));
        await db.SaveChangesAsync();

        return coopId;
    }

    private async Task<ProductionCycle> CycleAsync(Guid cycleId) =>
        await QueryAsync(db => db.ProductionCycles.AsNoTracking().SingleAsync(c => c.Id == cycleId));

    private static string Today() => DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private async Task<T> QueryAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, Dictionary<string, string> fields)
    {
        string page = await client.GetStringAsync(new Uri("/Finance/CostCenters/Create", UriKind.Relative));
        fields["__RequestVerificationToken"] = AntiforgeryToken(page);

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

    private static string ErrorOf(string html) => ErrorRegex().Match(html).Groups[1].Value.Trim();

    [GeneratedRegex("validation-summary-errors[^>]*>(.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex ErrorRegex();

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
