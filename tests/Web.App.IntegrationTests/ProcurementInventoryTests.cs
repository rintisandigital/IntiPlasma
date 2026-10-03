using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Domain.Procurement.PurchaseOrders;
using Infrastructure.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.App.Infrastructure.Export;

namespace Web.App.IntegrationTests;

/// <summary>
/// PhaseW4: purchase order → approve → goods receipt → stock → transfer → stock card, printed documents and exports.
/// </summary>
[Collection(nameof(WebAppCollection))]
public sealed partial class ProcurementInventoryTests(WebAppFactory factory)
{
    public static TheoryData<string> Pages =>
    [
        "/Procurement/PurchaseOrders", "/Procurement/PurchaseOrders/Create",
        "/Inventory/GoodsReceipts", "/Inventory/GoodsReceipts/Create",
        "/Inventory/StockTransfers", "/Inventory/StockTransfers/Create",
        "/Inventory/StockReturns", "/Inventory/StockReturns/Create",
        "/Inventory/FeedMutations",
        "/Inventory/Stock", "/Inventory/Stock/Card"
    ];

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Admin_Should_OpenPage(string path)
    {
        HttpClient client = await AdminClientAsync();

        (await client.GetAsync(new Uri(path, UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PurchaseOrder_Should_FlowThroughReceipt_StockAndTransfer()
    {
        HttpClient client = await AdminClientAsync();
        (string s, Guid branchId, Guid vendorId, Guid itemId, Guid kg, Guid sak, Guid warehouseA, Guid warehouseB) = await SetupAsync(client);

        // Item units lookup: base unit first, then the conversion
        using (var units = JsonDocument.Parse(await client.GetStringAsync(new Uri($"/Lookup/ItemUnits?itemId={itemId}", UriKind.Relative))))
        {
            units.RootElement.GetProperty("units").GetArrayLength().ShouldBe(2);
            units.RootElement.GetProperty("units")[0].GetProperty("value").GetGuid().ShouldBe(kg);
        }

        // Purchase order: 10 SAK at 400.000 → draft → approve
        HttpResponseMessage created = await PostAsync(client, "/Procurement/PurchaseOrders/Create", new()
        {
            ["BranchId"] = branchId.ToString(), ["VendorId"] = vendorId.ToString(), ["OrderDate"] = Today(),
            ["Lines[0].ItemId"] = itemId.ToString(), ["Lines[0].UomId"] = sak.ToString(),
            ["Lines[0].Quantity"] = "10", ["Lines[0].UnitPrice"] = "400000"
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var orderId = Guid.Parse(created.Headers.Location!.ToString().Split('/')[^1]);
        (await client.GetStringAsync(new Uri($"/Procurement/PurchaseOrders/Details/{orderId}", UriKind.Relative))).ShouldContain("4.000.000,00");

        await PostAsync(client, $"/Procurement/PurchaseOrders/Approve/{orderId}", []);
        (await QueryAsync(db => db.PurchaseOrders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync()))
            .ShouldBe(PurchaseOrderStatus.Approved);

        // Goods receipt of 6 SAK (= 300 KG) → partially received, stock 300 KG in A
        string receiptForm = await client.GetStringAsync(new Uri($"/Inventory/GoodsReceipts/Create?purchaseOrderId={orderId}", UriKind.Relative));
        receiptForm.ShouldContain($"GA{s}");
        HttpResponseMessage receipt = await PostAsync(client, "/Inventory/GoodsReceipts/Create", new()
        {
            ["PurchaseOrderId"] = orderId.ToString(), ["WarehouseId"] = warehouseA.ToString(), ["ReceiptDate"] = Today(),
            ["Lines[0].PurchaseOrderLineNumber"] = "1", ["Lines[0].Quantity"] = "6"
        });
        receipt.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var receiptId = Guid.Parse(receipt.Headers.Location!.ToString().Split('/')[^1]);
        (await QueryAsync(db => db.PurchaseOrders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync()))
            .ShouldBe(PurchaseOrderStatus.PartiallyReceived);
        (await StockAsync(warehouseA, itemId)).ShouldBe(300m);

        // Stock items lookup shows the stock on hand of warehouse A
        (await client.GetStringAsync(new Uri($"/Lookup/StockItems?warehouseId={warehouseA}&q=F{s}", UriKind.Relative))).ShouldContain("on hand 300");

        // Transfer 2 SAK (100 KG) from A to B
        HttpResponseMessage transfer = await PostAsync(client, "/Inventory/StockTransfers/Create", new()
        {
            ["FromWarehouseId"] = warehouseA.ToString(), ["ToWarehouseId"] = warehouseB.ToString(), ["Date"] = Today(),
            ["Lines[0].ItemId"] = itemId.ToString(), ["Lines[0].UomId"] = sak.ToString(), ["Lines[0].Quantity"] = "2"
        });
        transfer.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await StockAsync(warehouseA, itemId)).ShouldBe(200m);
        (await StockAsync(warehouseB, itemId)).ShouldBe(100m);

        // Stock card of A: receipt then transfer out
        string card = await client.GetStringAsync(new Uri($"/Inventory/Stock/Card?warehouseId={warehouseA}&itemId={itemId}", UriKind.Relative));
        card.ShouldContain("Opening balance");
        card.ShouldContain("BPB");
        card.ShouldContain("200,000");

        // Close the partially received order
        await PostAsync(client, $"/Procurement/PurchaseOrders/Close/{orderId}", []);
        (await QueryAsync(db => db.PurchaseOrders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync()))
            .ShouldBe(PurchaseOrderStatus.Closed);

        // Printed documents and exports
        await ShouldBePdfAsync(client, $"/Procurement/PurchaseOrders/Print/{orderId}");
        await ShouldBePdfAsync(client, $"/Inventory/GoodsReceipts/Print/{receiptId}");
        HttpResponseMessage export = await client.GetAsync(
            new Uri($"/Inventory/Stock/CardExport?format=xlsx&warehouseId={warehouseA}&itemId={itemId}&from={Today()}&to={Today()}", UriKind.Relative));
        export.Content.Headers.ContentType!.MediaType.ShouldBe(ExcelExporter.ContentType);
    }

    [Fact]
    public async Task PurchaseOrder_Should_BeCancelled_WithReason()
    {
        HttpClient client = await AdminClientAsync();
        (_, Guid branchId, Guid vendorId, Guid itemId, Guid kg, _, _, _) = await SetupAsync(client);
        HttpResponseMessage created = await PostAsync(client, "/Procurement/PurchaseOrders/Create", new()
        {
            ["BranchId"] = branchId.ToString(), ["VendorId"] = vendorId.ToString(), ["OrderDate"] = Today(),
            ["Lines[0].ItemId"] = itemId.ToString(), ["Lines[0].UomId"] = kg.ToString(),
            ["Lines[0].Quantity"] = "100", ["Lines[0].UnitPrice"] = "8000"
        });
        var orderId = Guid.Parse(created.Headers.Location!.ToString().Split('/')[^1]);

        HttpResponseMessage withoutReason = await PostAsync(client, $"/Procurement/PurchaseOrders/Cancel/{orderId}", []);
        withoutReason.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await StatusAsync(orderId)).ShouldBe(PurchaseOrderStatus.Draft);

        await PostAsync(client, $"/Procurement/PurchaseOrders/Cancel/{orderId}", new() { ["reason"] = "Vendor cannot deliver" });

        (await StatusAsync(orderId)).ShouldBe(PurchaseOrderStatus.Cancelled);
    }

    /// <summary>
    /// Branch, vendor, feed item (KG, 1 SAK = 50 KG) and two central warehouses, created through the web pages.
    /// </summary>
    private async Task<(string S, Guid BranchId, Guid VendorId, Guid ItemId, Guid Kg, Guid Sak, Guid WarehouseA, Guid WarehouseB)> SetupAsync(
        HttpClient client)
    {
        string s = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

        await PostAsync(client, "/Admin/Branches/Create", new() { ["Code"] = $"B{s}", ["Name"] = $"Branch {s}" });
        Guid branchId = await QueryAsync(db => db.Branches.Where(b => b.Code == $"B{s}").Select(b => b.Id).SingleAsync());
        await PostAsync(client, "/MasterData/Vendors/Create", new() { ["Code"] = $"V{s}", ["Name"] = $"Vendor {s}", ["PaymentTermDays"] = "30" });
        Guid vendorId = await QueryAsync(db => db.Vendors.Where(v => v.Code == $"V{s}").Select(v => v.Id).SingleAsync());
        Guid kg = await QueryAsync(db => db.Uoms.Where(u => u.Code == "KG").Select(u => u.Id).SingleAsync());
        Guid sak = await QueryAsync(db => db.Uoms.Where(u => u.Code == "SAK").Select(u => u.Id).SingleAsync());
        await PostAsync(client, "/MasterData/Items/Create", new()
        {
            ["Code"] = $"F{s}", ["Name"] = $"Feed {s}", ["Category"] = "Feed", ["BaseUomId"] = kg.ToString(),
            ["Conversions[0].UomId"] = sak.ToString(), ["Conversions[0].Factor"] = "50"
        });
        Guid itemId = await QueryAsync(db => db.Items.Where(i => i.Code == $"F{s}").Select(i => i.Id).SingleAsync());
        await PostAsync(client, "/MasterData/Warehouses/Create", new() { ["Code"] = $"GA{s}", ["Name"] = "Gudang A", ["BranchId"] = branchId.ToString() });
        await PostAsync(client, "/MasterData/Warehouses/Create", new() { ["Code"] = $"GB{s}", ["Name"] = "Gudang B", ["BranchId"] = branchId.ToString() });
        Guid warehouseA = await QueryAsync(db => db.Warehouses.Where(w => w.Code == $"GA{s}").Select(w => w.Id).SingleAsync());
        Guid warehouseB = await QueryAsync(db => db.Warehouses.Where(w => w.Code == $"GB{s}").Select(w => w.Id).SingleAsync());

        return (s, branchId, vendorId, itemId, kg, sak, warehouseA, warehouseB);
    }

    private async Task<PurchaseOrderStatus> StatusAsync(Guid orderId) =>
        await QueryAsync(db => db.PurchaseOrders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync());

    private static string Today() => DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private async Task<decimal> StockAsync(Guid warehouseId, Guid itemId) =>
        await QueryAsync(db => db.StockBalances.Where(b => b.WarehouseId == warehouseId && b.ItemId == itemId).Select(b => b.Quantity).SingleAsync());

    private static async Task ShouldBePdfAsync(HttpClient client, string path)
    {
        HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
    }

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

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
