using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Domain.Access;
using Domain.Costing.PlasmaSettlements;
using Domain.Finance.Payables;
using Domain.MasterData.Warehouses;
using Domain.Partnership.Contracts;
using Domain.Partnership.Cycles;
using Infrastructure.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Web.App.Infrastructure.Export;

namespace Web.App.IntegrationTests;

/// <summary>
/// PhaseW8: a closed plasma cycle (price contract) → cycle cost (list, breakdown, exports) → settlement draft
/// (cancelled with a reason, calculated again) → recalculated with a debt deduction → approved by the checker
/// (cycle settled) → paid through a plasma payment voucher; printed settlement and exports.
/// </summary>
[Collection(nameof(WebAppCollection))]
public sealed partial class CostingTests(WebAppFactory factory)
{
    private const string CheckerPassword = "Checker123!";

    public static TheoryData<string> Pages =>
    [
        "/Costing/CycleCosts", "/Costing/CycleCosts?status=Closed&branch=all", "/Costing/Settlements", "/Costing/Settlements/Create",
        "/Costing/Settlements?status=Draft&branch=all&search=STL"
    ];

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Admin_Should_OpenPage(string path)
    {
        HttpClient client = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);

        (await client.GetAsync(new Uri(path, UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PlasmaCycle_Should_BeCosted_Settled_Approved_AndPaid()
    {
        HttpClient admin = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        (string s, Guid cycleId, Guid farmerId, Guid cashBankId) = await ClosedPlasmaCycleAsync(admin);

        // Cycle cost: the closed cycle shows its final cost (100 DOC × 7.000) in the list, the breakdown and the exports
        string list = await admin.GetStringAsync(new Uri($"/Costing/CycleCosts?branch=all&search=K{s}", UriKind.Relative));
        list.ShouldContain($"K{s}");
        list.ShouldContain("Final");
        list.ShouldContain("700.000,00");
        string breakdown = await admin.GetStringAsync(new Uri($"/Costing/CycleCosts/Details/{cycleId}", UriKind.Relative));
        breakdown.ShouldContain($"D{s}");
        breakdown.ShouldContain("Create Settlement");
        HttpResponseMessage costExport = await admin.GetAsync(new Uri($"/Costing/CycleCosts/Export?format=xlsx&branch=all&search=K{s}", UriKind.Relative));
        costExport.Content.Headers.ContentType!.MediaType.ShouldBe(ExcelExporter.ContentType);
        await ShouldBePdfAsync(admin, $"/Costing/CycleCosts/DetailsExport/{cycleId}?format=pdf");
        (await admin.GetStringAsync(new Uri($"/Production/Cycles/Details/{cycleId}", UriKind.Relative))).ShouldContain("Create Settlement");

        // The lookup offers the closed plasma cycle; the form shows it with the farmer's (zero) plasma debt
        (await admin.GetStringAsync(new Uri($"/Lookup/SettleableCycles?q=K{s}", UriKind.Relative))).ShouldContain(cycleId.ToString());
        string form = await admin.GetStringAsync(new Uri($"/Costing/Settlements/Create?cycleId={cycleId}", UriKind.Relative));
        form.ShouldContain($"F{s}");
        form.ShouldContain("Plasma debt from earlier settlements");

        // A draft cancelled with a reason frees the cycle; the second draft is the one approved
        Guid cancelledId = IdOf(await PostAsync(admin, "/Costing/Settlements/Create", new()
        {
            ["CycleId"] = cycleId.ToString(), ["SettlementDate"] = Today(), ["DebtDeduction"] = "0"
        }));
        await PostAsync(admin, $"/Costing/Settlements/Cancel/{cancelledId}", []);
        (await SettlementAsync(cancelledId)).Status.ShouldBe(PlasmaSettlementStatus.Draft);
        await PostAsync(admin, $"/Costing/Settlements/Cancel/{cancelledId}", new() { ["reason"] = "Wrong date" });
        (await SettlementAsync(cancelledId)).Status.ShouldBe(PlasmaSettlementStatus.Cancelled);

        Guid settlementId = IdOf(await PostAsync(admin, "/Costing/Settlements/Create", new()
        {
            ["CycleId"] = cycleId.ToString(), ["SettlementDate"] = Today(), ["DebtDeduction"] = "0", ["Notes"] = "Cycle 1"
        }));

        // 200 kg × Rp 20.000 (BW 2,0) − 100 DOC × Rp 8.000 = Rp 3.200.000
        PlasmaSettlement draft = await SettlementAsync(settlementId);
        draft.GrossIncome.ShouldBe(new Money(3_200_000m));
        draft.NetPayable.ShouldBe(new Money(3_200_000m));
        string details = await admin.GetStringAsync(new Uri($"/Costing/Settlements/Details/{settlementId}", UriKind.Relative));
        details.ShouldContain("Live birds at the guaranteed price");
        details.ShouldContain("Sapronak at the contract price");
        details.ShouldContain("3.200.000,00");

        // Recalculate: a deduction above the payable is refused; 200.000 is deducted
        string refused = await (await PostAsync(admin, $"/Costing/Settlements/Recalculate/{settlementId}", new()
        {
            ["CycleId"] = cycleId.ToString(), ["SettlementDate"] = Today(), ["DebtDeduction"] = "5000000"
        })).Content.ReadAsStringAsync();
        refused.ShouldContain("debt deduction");
        await PostAsync(admin, $"/Costing/Settlements/Recalculate/{settlementId}", new()
        {
            ["CycleId"] = cycleId.ToString(), ["SettlementDate"] = Today(), ["DebtDeduction"] = "200000", ["Notes"] = "Cycle 1"
        });
        (await SettlementAsync(settlementId)).NetPayable.ShouldBe(new Money(3_000_000m));

        // The maker cannot approve; the checker can — the cycle becomes settled and leaves the lookup
        await PostAsync(admin, $"/Costing/Settlements/Approve/{settlementId}", []);
        (await SettlementAsync(settlementId)).Status.ShouldBe(PlasmaSettlementStatus.Draft);
        HttpClient checker = await CheckerClientAsync();
        await PostAsync(checker, $"/Costing/Settlements/Approve/{settlementId}", []);
        (await SettlementAsync(settlementId)).Status.ShouldBe(PlasmaSettlementStatus.Approved);
        (await QueryAsync(db => db.ProductionCycles.Where(c => c.Id == cycleId).Select(c => c.Status).SingleAsync())).ShouldBe(CycleStatus.Settled);
        (await admin.GetStringAsync(new Uri($"/Lookup/SettleableCycles?q=K{s}", UriKind.Relative))).ShouldNotContain(cycleId.ToString());
        (await admin.GetStringAsync(new Uri($"/Costing/Settlements/Details/{settlementId}", UriKind.Relative))).ShouldNotContain("/Costing/Settlements/Recalculate/");

        // Pay: the plasma payment voucher form has the settlement filled in; approved by the checker, paid
        string voucherForm = await admin.GetStringAsync(new Uri(
            $"/Finance/PaymentVouchers/Create?payeeType=Farmer&payeeId={farmerId}&documentId={settlementId}", UriKind.Relative));
        voucherForm.ShouldContain(draft.Number);
        Guid voucherId = IdOf(await PostAsync(admin, "/Finance/PaymentVouchers/Create", new()
        {
            ["PayeeType"] = "Farmer", ["PayeeId"] = farmerId.ToString(), ["CashBankAccountId"] = cashBankId.ToString(), ["PaymentDate"] = Today(),
            ["Allocations[0].DocumentId"] = settlementId.ToString(), ["Allocations[0].Amount"] = "1000000"
        }));
        await PostAsync(checker, $"/Finance/PaymentVouchers/Approve/{voucherId}", []);
        await PostAsync(admin, $"/Finance/PaymentVouchers/Pay/{voucherId}", new() { ["date"] = Today() });
        (await QueryAsync(db => db.PaymentVouchers.Where(v => v.Id == voucherId).Select(v => v.Status).SingleAsync())).ShouldBe(PaymentVoucherStatus.Paid);
        PlasmaSettlement paid = await SettlementAsync(settlementId);
        paid.Status.ShouldBe(PlasmaSettlementStatus.PartiallyPaid);
        paid.Outstanding.ShouldBe(new Money(2_000_000m));
        (await admin.GetStringAsync(new Uri($"/Finance/PaymentVouchers/Details/{voucherId}", UriKind.Relative)))
            .ShouldContain($"/Costing/Settlements/Details/{settlementId}");

        // The plasma income now shows in the cycle cost; printed settlement and list exports
        (await admin.GetStringAsync(new Uri($"/Costing/CycleCosts/Details/{cycleId}", UriKind.Relative))).ShouldContain("Total incl. plasma");
        await ShouldBePdfAsync(admin, $"/Costing/Settlements/Print/{settlementId}");
        HttpResponseMessage settlements = await admin.GetAsync(new Uri($"/Costing/Settlements/Export?format=pdf&branch=all&search=F{s}", UriKind.Relative));
        settlements.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        (await admin.GetStringAsync(new Uri($"/Costing/Settlements?branch=all&search=F{s}", UriKind.Relative))).ShouldContain(draft.Number);
    }

    [Fact]
    public async Task User_WithoutEditOrExport_Should_NotSeeSettlementActions()
    {
        HttpClient admin = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        (_, Guid cycleId, _, _) = await ClosedPlasmaCycleAsync(admin);
        Guid settlementId = IdOf(await PostAsync(admin, "/Costing/Settlements/Create", new()
        {
            ["CycleId"] = cycleId.ToString(), ["SettlementDate"] = Today(), ["DebtDeduction"] = "0"
        }));

        Guid profileId = await factory.CreateMenuProfileAsync(
            $"Settlement viewer {Guid.NewGuid():N}", (Web.App.Infrastructure.Authorization.MenuCodes.CostingSettlements, MenuRights.View));
        string email = $"viewer-{Guid.NewGuid():N}@intiplasma.test";
        await factory.CreateUserAsync(email, CheckerPassword, menuAccessProfileId: profileId, branchAccessProfileId: BranchAccessProfile.AllBranchesId);
        HttpClient viewer = await ClientAsync(email, CheckerPassword);

        string details = await viewer.GetStringAsync(new Uri($"/Costing/Settlements/Details/{settlementId}", UriKind.Relative));
        details.ShouldNotContain("/Costing/Settlements/Recalculate/");
        details.ShouldNotContain("Settlement PDF");
        ShouldBeForbidden(await viewer.GetAsync(new Uri($"/Costing/Settlements/Print/{settlementId}", UriKind.Relative)));
        ShouldBeForbidden(await PostAsync(viewer, $"/Costing/Settlements/Approve/{settlementId}", []));
        ShouldBeForbidden(await viewer.GetAsync(new Uri("/Costing/CycleCosts", UriKind.Relative)));
        (await SettlementAsync(settlementId)).Status.ShouldBe(PlasmaSettlementStatus.Draft);
    }

    /// <summary>
    /// Plasma farmer + coop under an active price contract (DOC Rp 8.000, live birds Rp 20.000/kg), a cycle of 100 DOC
    /// bought at Rp 7.000, harvested (100 birds / 200 kg), sold, invoiced and closed.
    /// </summary>
    private async Task<(string S, Guid CycleId, Guid FarmerId, Guid CashBankId)> ClosedPlasmaCycleAsync(HttpClient client)
    {
        string s = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        await PostAsync(client, "/Finance/FiscalPeriods/OpenYear", new() { ["year"] = DateTime.Today.Year.ToString(CultureInfo.InvariantCulture) });

        await PostAsync(client, "/Admin/Branches/Create", new() { ["Code"] = $"B{s}", ["Name"] = $"Branch {s}" });
        Guid branchId = await QueryAsync(db => db.Branches.Where(b => b.Code == $"B{s}").Select(b => b.Id).SingleAsync());
        Guid ekor = await QueryAsync(db => db.Uoms.Where(u => u.Code == "EKOR").Select(u => u.Id).SingleAsync());
        Guid kg = await QueryAsync(db => db.Uoms.Where(u => u.Code == "KG").Select(u => u.Id).SingleAsync());
        await PostAsync(client, "/MasterData/Items/Create", new() { ["Code"] = $"D{s}", ["Name"] = "DOC", ["Category"] = "Doc", ["BaseUomId"] = ekor.ToString() });
        await PostAsync(client, "/MasterData/Items/Create", new() { ["Code"] = $"A{s}", ["Name"] = "Ayam hidup", ["Category"] = "LiveBird", ["BaseUomId"] = kg.ToString() });
        Guid docId = await QueryAsync(db => db.Items.Where(i => i.Code == $"D{s}").Select(i => i.Id).SingleAsync());
        Guid birdId = await QueryAsync(db => db.Items.Where(i => i.Code == $"A{s}").Select(i => i.Id).SingleAsync());
        await PostAsync(client, "/MasterData/Vendors/Create", new() { ["Code"] = $"V{s}", ["Name"] = "Vendor", ["PaymentTermDays"] = "30" });
        Guid vendorId = await QueryAsync(db => db.Vendors.Where(v => v.Code == $"V{s}").Select(v => v.Id).SingleAsync());
        await PostAsync(client, "/MasterData/Customers/Create", new() { ["Code"] = $"C{s}", ["Name"] = "Bakul", ["PaymentTermDays"] = "14", ["CreditLimit"] = "100000000" });
        Guid customerId = await QueryAsync(db => db.Customers.Where(c => c.Code == $"C{s}").Select(c => c.Id).SingleAsync());

        await PostAsync(client, "/Partnership/Farmers/Create", new()
        {
            ["Code"] = $"F{s}", ["Name"] = $"Plasma {s}", ["Type"] = "Plasma", ["BranchId"] = branchId.ToString(), ["Nik"] = "3201010101010001"
        });
        Guid farmerId = await QueryAsync(db => db.Farmers.Where(f => f.Code == $"F{s}").Select(f => f.Id).SingleAsync());
        await PostAsync(client, "/Partnership/Coops/Create", new()
        {
            ["FarmerId"] = farmerId.ToString(), ["Code"] = $"K{s}", ["Name"] = "Coop", ["Capacity"] = "5000", ["HouseType"] = "ClosedHouse"
        });
        Guid coopId = await QueryAsync(db => db.Coops.Where(c => c.Code == $"K{s}").Select(c => c.Id).SingleAsync());
        await AddAsync(Warehouse.CreateForCoop($"GK-K{s}", "Gudang Kandang", branchId, coopId, null));
        Guid coopWarehouseId = await QueryAsync(db => db.Warehouses.Where(w => w.CoopId == coopId).Select(w => w.Id).SingleAsync());

        PartnershipContract contract = PartnershipContract.Create($"KTR{s}", branchId, ContractScheme.PriceContract, new ContractTerms(
            "Price contract", DateOnly.FromDateTime(DateTime.Today).AddDays(-30), null, null, null, null,
            [new ContractTerms.InputPrice(docId, new Money(8_000m))],
            [new ContractTerms.LiveBirdPrice(0m, 5m, new Money(20_000m))],
            [])).Value;
        contract.Activate().IsSuccess.ShouldBeTrue();
        await AddAsync(contract);

        Guid ledgerAccountId = await AccountAsync(client, $"1-19{s}", $"Bank {s}");
        await PostAsync(client, "/Finance/CashBankAccounts/Create", new()
        {
            ["Code"] = $"BK{s}", ["Name"] = "Bank", ["Type"] = "Bank", ["BranchId"] = branchId.ToString(), ["AccountId"] = ledgerAccountId.ToString(),
            ["BankName"] = "BCA", ["AccountNumber"] = "123"
        });
        Guid cashBankId = await QueryAsync(db => db.CashBankAccounts.Where(c => c.Code == $"BK{s}").Select(c => c.Id).SingleAsync());

        // Plan → 100 DOC into the coop → chick-in → harvest 100 birds / 200 kg
        Guid cycleId = IdOf(await PostAsync(client, "/Production/Cycles/Create", new()
        {
            ["CoopId"] = coopId.ToString(), ["ContractId"] = contract.Id.ToString(), ["PlannedChickInDate"] = Today(), ["PlannedPopulation"] = "100"
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

        // Before closing the list shows the running cost, the same as the breakdown: 700.000 / 200 kg = 3.500 per kg
        string running = await client.GetStringAsync(new Uri($"/Costing/CycleCosts?branch=all&search=K{s}", UriKind.Relative));
        running.ShouldContain("Running");
        running.ShouldContain("Rp 3.500,00");
        (await client.GetStringAsync(new Uri($"/Costing/CycleCosts/Details/{cycleId}", UriKind.Relative))).ShouldContain("Rp 3.500,00");

        // Sold: order → delivery → invoice posted; then the cycle is closed
        Guid soId = IdOf(await PostAsync(client, "/Sales/SalesOrders/Create", new()
        {
            ["BranchId"] = branchId.ToString(), ["CustomerId"] = customerId.ToString(), ["OrderDate"] = Today(),
            ["Lines[0].ItemId"] = birdId.ToString(), ["Lines[0].Birds"] = "100", ["Lines[0].EstimatedWeightKg"] = "200", ["Lines[0].PricePerKg"] = "22000"
        }));
        await PostAsync(client, $"/Sales/SalesOrders/Approve/{soId}", []);
        Guid doId = IdOf(await PostAsync(client, "/Sales/DeliveryOrders/Create", new()
        {
            ["SalesOrderId"] = soId.ToString(), ["DeliveryDate"] = Today(), ["VehicleNumber"] = "D 1 AA",
            ["Lines[0].HarvestId"] = harvestId.ToString(), ["Lines[0].Selected"] = "true", ["Lines[0].SalesOrderLineNumber"] = "1"
        }));
        Guid invoiceId = IdOf(await PostAsync(client, "/Sales/SalesInvoices/Create", new()
        {
            ["CustomerId"] = customerId.ToString(), ["InvoiceDate"] = Today(), ["DeliveryOrderIds"] = doId.ToString()
        }));
        await PostAsync(client, $"/Sales/SalesInvoices/Post/{invoiceId}", []);
        await PostAsync(client, $"/Production/Cycles/Close/{cycleId}", []);
        (await QueryAsync(db => db.ProductionCycles.Where(c => c.Id == cycleId).Select(c => c.Status).SingleAsync())).ShouldBe(CycleStatus.Closed);

        return (s, cycleId, farmerId, cashBankId);
    }

    private async Task<Guid> AccountAsync(HttpClient client, string code, string name)
    {
        await PostAsync(client, "/Finance/Accounts/Create", new() { ["Code"] = code, ["Name"] = name, ["Type"] = "Asset", ["IsPostable"] = "true" });
        return await QueryAsync(db => db.Accounts.Where(a => a.Code == code).Select(a => a.Id).SingleAsync());
    }

    /// <summary>
    /// A second user (Full Access, all branches) acting as the checker of maker-checker documents.
    /// </summary>
    private async Task<HttpClient> CheckerClientAsync()
    {
        string email = $"checker-{Guid.NewGuid():N}@intiplasma.test";
        await factory.CreateUserAsync(email, CheckerPassword, branchAccessProfileId: BranchAccessProfile.AllBranchesId);
        return await ClientAsync(email, CheckerPassword);
    }

    private async Task<PlasmaSettlement> SettlementAsync(Guid id) =>
        await QueryAsync(db => db.PlasmaSettlements.AsNoTracking().SingleAsync(s => s.Id == id));

    private static Guid IdOf(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        string path = response.Headers.Location!.ToString().Split('?')[0];
        return Guid.Parse(path[(path.LastIndexOf('/') + 1)..]);
    }

    private static void ShouldBeForbidden(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldContain("/Error/403");
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
        string page = await client.GetStringAsync(new Uri("/Account/ChangePassword", UriKind.Relative));
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
