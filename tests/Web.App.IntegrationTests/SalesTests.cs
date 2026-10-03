using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Domain.Finance.Receivables;
using Domain.MasterData.Warehouses;
using Domain.Sales.SalesInvoices;
using Domain.Sales.SalesOrders;
using Infrastructure.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.App.Infrastructure.Export;

namespace Web.App.IntegrationTests;

/// <summary>
/// PhaseW6: harvest → sales order (credit limit + override) → delivery order → invoice (draft → post) → credit note
/// → receipt with advance → advance applied → void, receivable ledger/aging, printed documents and exports.
/// </summary>
[Collection(nameof(WebAppCollection))]
public sealed partial class SalesTests(WebAppFactory factory)
{
    public static TheoryData<string> Pages =>
    [
        "/Sales/SalesOrders", "/Sales/SalesOrders/Create", "/Sales/DeliveryOrders", "/Sales/DeliveryOrders/Create",
        "/Sales/SalesInvoices", "/Sales/SalesInvoices/Create", "/Sales/CreditNotes", "/Sales/Receipts", "/Sales/Receipts/Create",
        "/Sales/Receivables", "/Sales/Receivables?tab=ledger"
    ];

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Admin_Should_OpenPage(string path)
    {
        HttpClient client = await AdminClientAsync();

        (await client.GetAsync(new Uri(path, UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Harvest_Should_BeSold_Invoiced_Credited_AndPaid()
    {
        HttpClient client = await AdminClientAsync();
        string s = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        string year = DateTime.Today.Year.ToString(CultureInfo.InvariantCulture);
        await PostAsync(client, "/Finance/FiscalPeriods/OpenYear", new() { ["year"] = year }); // may already be open

        // Branch, items, farmer, coop (+ warehouse), customer with a small credit limit, cash/bank account
        await PostAsync(client, "/Admin/Branches/Create", new() { ["Code"] = $"B{s}", ["Name"] = $"Branch {s}" });
        Guid branchId = await QueryAsync(db => db.Branches.Where(b => b.Code == $"B{s}").Select(b => b.Id).SingleAsync());
        Guid ekor = await UomAsync("EKOR");
        Guid kg = await UomAsync("KG");
        await PostAsync(client, "/MasterData/Items/Create", new() { ["Code"] = $"D{s}", ["Name"] = "DOC", ["Category"] = "Doc", ["BaseUomId"] = ekor.ToString() });
        await PostAsync(client, "/MasterData/Items/Create", new() { ["Code"] = $"A{s}", ["Name"] = "Ayam hidup", ["Category"] = "LiveBird", ["BaseUomId"] = kg.ToString() });
        Guid docId = await QueryAsync(db => db.Items.Where(i => i.Code == $"D{s}").Select(i => i.Id).SingleAsync());
        Guid birdId = await QueryAsync(db => db.Items.Where(i => i.Code == $"A{s}").Select(i => i.Id).SingleAsync());
        await PostAsync(client, "/MasterData/Vendors/Create", new() { ["Code"] = $"V{s}", ["Name"] = "Vendor", ["PaymentTermDays"] = "30" });
        Guid vendorId = await QueryAsync(db => db.Vendors.Where(v => v.Code == $"V{s}").Select(v => v.Id).SingleAsync());
        await PostAsync(client, "/MasterData/Customers/Create", new() { ["Code"] = $"C{s}", ["Name"] = "Bakul", ["PaymentTermDays"] = "14", ["CreditLimit"] = "1000000" });
        Guid customerId = await QueryAsync(db => db.Customers.Where(c => c.Code == $"C{s}").Select(c => c.Id).SingleAsync());
        await PostAsync(client, "/Partnership/Farmers/Create", new() { ["Code"] = $"F{s}", ["Name"] = "Inti", ["Type"] = "Inti", ["BranchId"] = branchId.ToString() });
        Guid farmerId = await QueryAsync(db => db.Farmers.Where(f => f.Code == $"F{s}").Select(f => f.Id).SingleAsync());
        await PostAsync(client, "/Partnership/Coops/Create", new()
        {
            ["FarmerId"] = farmerId.ToString(), ["Code"] = $"K{s}", ["Name"] = "Coop", ["Capacity"] = "5000", ["HouseType"] = "ClosedHouse"
        });
        Guid coopId = await QueryAsync(db => db.Coops.Where(c => c.Code == $"K{s}").Select(c => c.Id).SingleAsync());
        await AddAsync(Warehouse.CreateForCoop($"GK-K{s}", "Gudang Kandang", branchId, coopId, null));
        Guid coopWarehouseId = await QueryAsync(db => db.Warehouses.Where(w => w.CoopId == coopId).Select(w => w.Id).SingleAsync());
        await PostAsync(client, "/Finance/Accounts/Create", new() { ["Code"] = $"1-19{s}", ["Name"] = $"Bank {s}", ["Type"] = "Asset", ["IsPostable"] = "true" });
        Guid ledgerAccountId = await QueryAsync(db => db.Accounts.Where(a => a.Code == $"1-19{s}").Select(a => a.Id).SingleAsync());
        await PostAsync(client, "/Finance/CashBankAccounts/Create", new()
        {
            ["Code"] = $"BK{s}", ["Name"] = "Bank", ["Type"] = "Bank", ["BranchId"] = branchId.ToString(), ["AccountId"] = ledgerAccountId.ToString(),
            ["BankName"] = "BCA", ["AccountNumber"] = "123"
        });
        Guid cashBankId = await QueryAsync(db => db.CashBankAccounts.Where(c => c.Code == $"BK{s}").Select(c => c.Id).SingleAsync());

        // Cycle: plan → DOC into the coop → chick-in → harvest of 100 birds / 200 kg
        Guid cycleId = IdOf(await PostAsync(client, "/Production/Cycles/Create", new()
        {
            ["CoopId"] = coopId.ToString(), ["PlannedChickInDate"] = Today(), ["PlannedPopulation"] = "100"
        }));
        Guid poId = IdOf(await PostAsync(client, "/Procurement/PurchaseOrders/Create", new()
        {
            ["BranchId"] = branchId.ToString(), ["VendorId"] = vendorId.ToString(), ["OrderDate"] = Today(),
            ["Lines[0].ItemId"] = docId.ToString(), ["Lines[0].UomId"] = ekor.ToString(), ["Lines[0].Quantity"] = "100", ["Lines[0].UnitPrice"] = "7000"
        }));
        await PostAsync(client, $"/Procurement/PurchaseOrders/Approve/{poId}", []);
        await PostAsync(client, "/Inventory/GoodsReceipts/Create", new()
        {
            ["PurchaseOrderId"] = poId.ToString(), ["WarehouseId"] = coopWarehouseId.ToString(), ["ReceiptDate"] = Today(),
            ["Lines[0].PurchaseOrderLineNumber"] = "1", ["Lines[0].Quantity"] = "100"
        });
        await PostAsync(client, $"/Production/Cycles/ChickIn/{cycleId}", new()
        {
            ["ChickInDate"] = Today(), ["Lines[0].ItemId"] = docId.ToString(), ["Lines[0].Quantity"] = "100"
        });
        await PostAsync(client, "/Production/Harvests/Create", new()
        {
            ["Harvest.CycleId"] = cycleId.ToString(), ["Harvest.Date"] = Today(), ["Harvest.Birds"] = "100", ["Harvest.WeightKg"] = "200"
        });
        Guid harvestId = await QueryAsync(db => db.ProductionCycles.Where(c => c.Id == cycleId).SelectMany(c => c.Harvests).Select(h => h.Id).SingleAsync());

        // Sales order of Rp 4.000.000 against a credit limit of Rp 1.000.000 → refused, then approved with a reason
        Guid soId = IdOf(await PostAsync(client, "/Sales/SalesOrders/Create", new()
        {
            ["BranchId"] = branchId.ToString(), ["CustomerId"] = customerId.ToString(), ["OrderDate"] = Today(),
            ["Lines[0].ItemId"] = birdId.ToString(), ["Lines[0].Birds"] = "100", ["Lines[0].EstimatedWeightKg"] = "200", ["Lines[0].PricePerKg"] = "20000"
        }));
        string details = await client.GetStringAsync(new Uri($"/Sales/SalesOrders/Details/{soId}", UriKind.Relative));
        details.ShouldContain("Customer credit");
        details.ShouldContain("Approve over limit");

        await PostAsync(client, $"/Sales/SalesOrders/Approve/{soId}", []);
        (await QueryAsync(db => db.SalesOrders.Where(o => o.Id == soId).Select(o => o.Status).SingleAsync())).ShouldBe(SalesOrderStatus.Draft);
        await PostAsync(client, $"/Sales/SalesOrders/ApproveOverLimit/{soId}", new() { ["reason"] = "Long-standing customer" });
        (await QueryAsync(db => db.SalesOrders.Where(o => o.Id == soId).Select(o => o.Status).SingleAsync())).ShouldBe(SalesOrderStatus.Approved);

        // Delivery order of the harvest → invoice draft → post
        string deliveryForm = await client.GetStringAsync(new Uri($"/Sales/DeliveryOrders/Create?salesOrderId={soId}", UriKind.Relative));
        deliveryForm.ShouldContain(harvestId.ToString());
        Guid doId = IdOf(await PostAsync(client, "/Sales/DeliveryOrders/Create", new()
        {
            ["SalesOrderId"] = soId.ToString(), ["DeliveryDate"] = Today(), ["VehicleNumber"] = "D 1 AA",
            ["Lines[0].HarvestId"] = harvestId.ToString(), ["Lines[0].Selected"] = "true", ["Lines[0].SalesOrderLineNumber"] = "1"
        }));
        Guid invoiceId = IdOf(await PostAsync(client, "/Sales/SalesInvoices/Create", new()
        {
            ["CustomerId"] = customerId.ToString(), ["InvoiceDate"] = Today(), ["DeliveryOrderIds"] = doId.ToString()
        }));
        (await InvoiceAsync(invoiceId)).Status.ShouldBe(SalesInvoiceStatus.Draft);
        await PostAsync(client, $"/Sales/SalesInvoices/Post/{invoiceId}", []);
        SalesInvoice posted = await InvoiceAsync(invoiceId);
        posted.Status.ShouldBe(SalesInvoiceStatus.Posted);
        posted.Total.Amount.ShouldBe(4_000_000m);

        // Credit note of Rp 100.000, then a receipt of 3.000.000 on the invoice + 500.000 advance
        await PostAsync(client, "/Sales/CreditNotes/Create", new()
        {
            ["SalesInvoiceId"] = invoiceId.ToString(), ["Date"] = Today(), ["Reason"] = "Weight correction",
            ["Lines[0].InvoiceLineNumber"] = "1", ["Lines[0].Amount"] = "100000"
        });
        (await InvoiceAsync(invoiceId)).Outstanding.Amount.ShouldBe(3_900_000m);

        Guid receiptId = IdOf(await PostAsync(client, "/Sales/Receipts/Create", new()
        {
            ["CustomerId"] = customerId.ToString(), ["CashBankAccountId"] = cashBankId.ToString(), ["ReceiptDate"] = Today(),
            ["Reference"] = "TRF-1", ["AdvanceAmount"] = "500000",
            ["Allocations[0].SalesInvoiceId"] = invoiceId.ToString(), ["Allocations[0].Amount"] = "3000000"
        }));
        (await InvoiceAsync(invoiceId)).Status.ShouldBe(SalesInvoiceStatus.PartiallyPaid);

        // The advance closes the invoice
        await PostAsync(client, $"/Sales/Receipts/ApplyAdvance/{receiptId}", new()
        {
            ["date"] = Today(), ["allocations[0].SalesInvoiceId"] = invoiceId.ToString(), ["allocations[0].Amount"] = "500000"
        });
        SalesInvoice afterAdvance = await InvoiceAsync(invoiceId);
        afterAdvance.Outstanding.Amount.ShouldBe(400_000m);

        // Ledger and aging
        string ledger = await client.GetStringAsync(new Uri(
            $"/Sales/Receivables?tab=ledger&customerId={customerId}&branch=all&from={Today()}&to={Today()}", UriKind.Relative));
        ledger.ShouldContain("Closing balance");
        ledger.ShouldContain("400.000,00");
        (await client.GetStringAsync(new Uri($"/Sales/Receivables?customerId={customerId}&branch=all", UriKind.Relative))).ShouldContain($"C{s}");

        // Printed documents and exports
        await ShouldBePdfAsync(client, $"/Sales/DeliveryOrders/Print/{doId}");
        await ShouldBePdfAsync(client, $"/Sales/SalesInvoices/Print/{invoiceId}");
        await ShouldBePdfAsync(client, $"/Sales/Receipts/Print/{receiptId}");
        Guid creditNoteId = await QueryAsync(db => db.SalesCreditNotes.Where(c => c.SalesInvoiceId == invoiceId).Select(c => c.Id).SingleAsync());
        await ShouldBePdfAsync(client, $"/Sales/CreditNotes/Print/{creditNoteId}");
        HttpResponseMessage aging = await client.GetAsync(new Uri($"/Sales/Receivables/AgingExport?format=xlsx&branch=all&asOf={Today()}", UriKind.Relative));
        aging.Content.Headers.ContentType!.MediaType.ShouldBe(ExcelExporter.ContentType);

        // A receipt whose advance was applied cannot be voided (the void button is hidden)
        (await client.GetStringAsync(new Uri($"/Sales/Receipts/Details/{receiptId}", UriKind.Relative))).ShouldNotContain("void-modal\"><i");
        await PostAsync(client, $"/Sales/Receipts/Void/{receiptId}", new() { ["date"] = Today(), ["reason"] = "Bounced transfer" });
        (await ReceiptStatusAsync(receiptId)).ShouldBe(CustomerReceiptStatus.Posted);

        // A second receipt pays the rest; voiding it (bounced giro) puts the outstanding back on the invoice
        Guid secondId = IdOf(await PostAsync(client, "/Sales/Receipts/Create", new()
        {
            ["CustomerId"] = customerId.ToString(), ["CashBankAccountId"] = cashBankId.ToString(), ["ReceiptDate"] = Today(), ["Reference"] = "GIRO-2",
            ["Allocations[0].SalesInvoiceId"] = invoiceId.ToString(), ["Allocations[0].Amount"] = "400000"
        }));
        (await InvoiceAsync(invoiceId)).Status.ShouldBe(SalesInvoiceStatus.Paid);
        await PostAsync(client, $"/Sales/Receipts/Void/{secondId}", new() { ["date"] = Today(), ["reason"] = "Bounced giro" });
        (await ReceiptStatusAsync(secondId)).ShouldBe(CustomerReceiptStatus.Voided);
        (await InvoiceAsync(invoiceId)).Outstanding.Amount.ShouldBe(400_000m);
    }

    private async Task<CustomerReceiptStatus> ReceiptStatusAsync(Guid id) =>
        await QueryAsync(db => db.CustomerReceipts.Where(r => r.Id == id).Select(r => r.Status).SingleAsync());

    private async Task<SalesInvoice> InvoiceAsync(Guid id) =>
        await QueryAsync(db => db.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == id));

    private async Task<Guid> UomAsync(string code) =>
        await QueryAsync(db => db.Uoms.Where(u => u.Code == code).Select(u => u.Id).SingleAsync());

    private static Guid IdOf(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        string path = response.Headers.Location!.ToString().Split('?')[0];
        return Guid.Parse(path[(path.LastIndexOf('/') + 1)..]);
    }

    private static string Today() => DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task ShouldBePdfAsync(HttpClient client, string path)
    {
        HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        System.Text.Encoding.ASCII.GetString(await response.Content.ReadAsByteArrayAsync(), 0, 5).ShouldBe("%PDF-");
    }

    private async Task AddAsync(object entity)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Add(entity);
        await db.SaveChangesAsync();
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
