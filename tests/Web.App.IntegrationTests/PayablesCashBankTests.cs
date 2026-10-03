using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Domain.Access;
using Domain.Finance.CashBank;
using Domain.Finance.Payables;
using Infrastructure.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;

namespace Web.App.IntegrationTests;

/// <summary>
/// PhaseW7: goods receipt → vendor invoice (price variance needs a reason) → payment voucher (maker-checker) → paid,
/// cash in/out (cash-out approved by another user), bank transfer, cash/bank book, bank reconciliation, payable
/// ledger/aging, printed vouchers and exports.
/// </summary>
[Collection(nameof(WebAppCollection))]
public sealed partial class PayablesCashBankTests(WebAppFactory factory)
{
    private const string CheckerPassword = "Checker123!";

    public static TheoryData<string> Pages =>
    [
        "/Finance/VendorInvoices", "/Finance/VendorInvoices/Create", "/Finance/PaymentVouchers", "/Finance/PaymentVouchers/Create",
        "/Finance/PaymentVouchers/Create?payeeType=Farmer", "/Finance/CashTransactions", "/Finance/CashTransactions/Create?direction=In",
        "/Finance/CashTransactions/Create?direction=Out", "/Finance/BankTransfers", "/Finance/BankTransfers/Create",
        "/Finance/BankReconciliations", "/Finance/BankReconciliations/Create", "/Finance/Payables", "/Finance/Payables?tab=ledger"
    ];

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Admin_Should_OpenPage(string path)
    {
        HttpClient client = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);

        (await client.GetAsync(new Uri(path, UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Every released menu of the sidebar points to an existing page.
    /// </summary>
    [Fact]
    public async Task Admin_Should_OpenEveryReleasedMenu()
    {
        HttpClient client = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);

        foreach (MenuDefinition menu in MenuCatalog.Definitions.Where(m => m.IsAvailable && m.Route is not null))
        {
            HttpResponseMessage response = await client.GetAsync(new Uri(menu.Route!, UriKind.Relative));
            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{menu.Code} → {menu.Route}");
        }
    }

    [Fact]
    public async Task VendorInvoice_Should_BePosted_WithVariance_AndPaidByApprovedVoucher()
    {
        HttpClient admin = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        (string s, Guid branchId, Guid cashBankId, Guid vendorId) = await SetupAsync(admin);

        // Goods receipt of 100 KG at 8.000 → not yet billed
        Guid itemId = await QueryAsync(db => db.Items.Where(i => i.Code == $"F{s}").Select(i => i.Id).SingleAsync());
        Guid kg = await QueryAsync(db => db.Uoms.Where(u => u.Code == "KG").Select(u => u.Id).SingleAsync());
        Guid warehouseId = await QueryAsync(db => db.Warehouses.Where(w => w.Code == $"GA{s}").Select(w => w.Id).SingleAsync());
        Guid poId = IdOf(await PostAsync(admin, "/Procurement/PurchaseOrders/Create", new()
        {
            ["BranchId"] = branchId.ToString(), ["VendorId"] = vendorId.ToString(), ["OrderDate"] = Today(),
            ["Lines[0].ItemId"] = itemId.ToString(), ["Lines[0].UomId"] = kg.ToString(), ["Lines[0].Quantity"] = "100", ["Lines[0].UnitPrice"] = "8000"
        }));
        await PostAsync(admin, $"/Procurement/PurchaseOrders/Approve/{poId}", []);
        Guid receiptId = IdOf(await PostAsync(admin, "/Inventory/GoodsReceipts/Create", new()
        {
            ["PurchaseOrderId"] = poId.ToString(), ["WarehouseId"] = warehouseId.ToString(), ["ReceiptDate"] = Today(),
            ["Lines[0].PurchaseOrderLineNumber"] = "1", ["Lines[0].Quantity"] = "100"
        }));

        // The vendor invoice form lists the receipt line, billed in full at the order price
        string form = await admin.GetStringAsync(new Uri($"/Finance/VendorInvoices/Create?vendorId={vendorId}&branchId={branchId}", UriKind.Relative));
        form.ShouldContain(receiptId.ToString());

        // A draft is cancelled with a reason; its receipt line can be billed again
        Guid cancelledId = IdOf(await PostAsync(admin, "/Finance/VendorInvoices/Create", new()
        {
            ["VendorId"] = vendorId.ToString(), ["BranchId"] = branchId.ToString(), ["VendorInvoiceNumber"] = $"DUP-{s}", ["InvoiceDate"] = Today(),
            ["Lines[0].GoodsReceiptId"] = receiptId.ToString(), ["Lines[0].GoodsReceiptLineNumber"] = "1", ["Lines[0].Selected"] = "true",
            ["Lines[0].Quantity"] = "100", ["Lines[0].UnitPrice"] = "8000"
        }));
        await PostAsync(admin, $"/Finance/VendorInvoices/Cancel/{cancelledId}", []);
        (await InvoiceAsync(cancelledId)).Status.ShouldBe(VendorInvoiceStatus.Draft);
        await PostAsync(admin, $"/Finance/VendorInvoices/Cancel/{cancelledId}", new() { ["reason"] = "Duplicate entry" });
        (await InvoiceAsync(cancelledId)).Status.ShouldBe(VendorInvoiceStatus.Cancelled);

        // Vendor bills 8.500/kg (above the 0% tolerance) → draft → post refused → post with a reason
        Guid invoiceId = IdOf(await PostAsync(admin, "/Finance/VendorInvoices/Create", new()
        {
            ["VendorId"] = vendorId.ToString(), ["BranchId"] = branchId.ToString(), ["VendorInvoiceNumber"] = $"INV-{s}", ["InvoiceDate"] = Today(),
            ["Lines[0].GoodsReceiptId"] = receiptId.ToString(), ["Lines[0].GoodsReceiptLineNumber"] = "1", ["Lines[0].Selected"] = "true",
            ["Lines[0].Quantity"] = "100", ["Lines[0].UnitPrice"] = "8500"
        }));
        (await InvoiceAsync(invoiceId)).Status.ShouldBe(VendorInvoiceStatus.Draft);

        await PostAsync(admin, $"/Finance/VendorInvoices/Post/{invoiceId}", []);
        (await InvoiceAsync(invoiceId)).Status.ShouldBe(VendorInvoiceStatus.Draft);
        (await admin.GetStringAsync(new Uri($"/Finance/VendorInvoices/Details/{invoiceId}", UriKind.Relative))).ShouldContain("Post with variance");

        await PostAsync(admin, $"/Finance/VendorInvoices/PostWithVariance/{invoiceId}", new() { ["reason"] = "Price increase agreed by phone" });
        VendorInvoice posted = await InvoiceAsync(invoiceId);
        posted.Status.ShouldBe(VendorInvoiceStatus.Posted);
        posted.Total.Amount.ShouldBe(850_000m);

        // Payment voucher (maker = admin): the maker cannot approve it, the checker can; then it is paid
        string voucherForm = await admin.GetStringAsync(new Uri($"/Finance/PaymentVouchers/Create?payeeId={vendorId}&documentId={invoiceId}", UriKind.Relative));
        voucherForm.ShouldContain($"INV-{s}");
        Guid voucherId = IdOf(await PostAsync(admin, "/Finance/PaymentVouchers/Create", new()
        {
            ["PayeeType"] = "Vendor", ["PayeeId"] = vendorId.ToString(), ["CashBankAccountId"] = cashBankId.ToString(), ["PaymentDate"] = Today(),
            ["Reference"] = "TRF-1", ["Allocations[0].DocumentId"] = invoiceId.ToString(), ["Allocations[0].Amount"] = "850000"
        }));
        (await VoucherStatusAsync(voucherId)).ShouldBe(PaymentVoucherStatus.Draft);

        await PostAsync(admin, $"/Finance/PaymentVouchers/Approve/{voucherId}", []);
        (await VoucherStatusAsync(voucherId)).ShouldBe(PaymentVoucherStatus.Draft);

        HttpClient checker = await CheckerClientAsync();
        await PostAsync(checker, $"/Finance/PaymentVouchers/Approve/{voucherId}", []);
        (await VoucherStatusAsync(voucherId)).ShouldBe(PaymentVoucherStatus.Approved);

        await PostAsync(admin, $"/Finance/PaymentVouchers/Pay/{voucherId}", new() { ["date"] = Today() });
        (await VoucherStatusAsync(voucherId)).ShouldBe(PaymentVoucherStatus.Paid);
        (await InvoiceAsync(invoiceId)).Status.ShouldBe(VendorInvoiceStatus.Paid);

        // Ledger, aging, printed voucher and exports
        string ledger = await admin.GetStringAsync(new Uri(
            $"/Finance/Payables?tab=ledger&vendorId={vendorId}&branch=all&from={Today()}&to={Today()}", UriKind.Relative));
        ledger.ShouldContain("Closing balance");
        ledger.ShouldContain("850.000,00");
        (await admin.GetStringAsync(new Uri($"/Finance/Payables?vendorId={vendorId}&branch=all", UriKind.Relative))).ShouldContain("No outstanding payable.");
        await ShouldBePdfAsync(admin, $"/Finance/PaymentVouchers/Print/{voucherId}");
        HttpResponseMessage aging = await admin.GetAsync(new Uri($"/Finance/Payables/AgingExport?format=xlsx&branch=all&asOf={Today()}", UriKind.Relative));
        aging.Content.Headers.ContentType!.MediaType.ShouldBe(ExcelExporter.ContentType);
        HttpResponseMessage list = await admin.GetAsync(new Uri("/Finance/VendorInvoices/Export?format=pdf&branch=all", UriKind.Relative));
        list.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");

    }

    [Fact]
    public async Task CashInOut_Transfer_AndReconciliation_Should_Work()
    {
        HttpClient admin = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        (string s, Guid branchId, Guid bankId, _) = await SetupAsync(admin);
        Guid pettyCashId = await CashBankAsync(admin, s, branchId, "KK", "PettyCash");
        Guid incomeAccountId = await AccountAsync(admin, $"4-9{s}", $"Other income {s}", "Revenue");
        Guid expenseAccountId = await AccountAsync(admin, $"6-9{s}", $"Electricity {s}", "Expense");

        // Cash in: draft → posted right away (BKM)
        Guid cashInId = IdOf(await PostAsync(admin, "/Finance/CashTransactions/Create", new()
        {
            ["Direction"] = "In", ["CashBankAccountId"] = bankId.ToString(), ["Date"] = Today(), ["Description"] = "Bank interest",
            ["Lines[0].AccountId"] = incomeAccountId.ToString(), ["Lines[0].Amount"] = "250000"
        }));
        await PostAsync(admin, $"/Finance/CashTransactions/Post/{cashInId}", []);
        CashTransaction cashIn = await CashAsync(cashInId);
        cashIn.Status.ShouldBe(CashTransactionStatus.Posted);
        cashIn.Number!.ShouldStartWith("BKM/");

        // Cash out: the maker cannot approve; the checker approves; then it is posted (BKK)
        Guid cashOutId = IdOf(await PostAsync(admin, "/Finance/CashTransactions/Create", new()
        {
            ["Direction"] = "Out", ["CashBankAccountId"] = bankId.ToString(), ["Date"] = Today(), ["Description"] = "Electricity bill",
            ["Lines[0].AccountId"] = expenseAccountId.ToString(), ["Lines[0].Amount"] = "100000", ["Lines[0].Description"] = "PLN"
        }));
        await PostAsync(admin, $"/Finance/CashTransactions/Post/{cashOutId}", []);
        (await CashAsync(cashOutId)).Status.ShouldBe(CashTransactionStatus.Draft);
        await PostAsync(admin, $"/Finance/CashTransactions/Approve/{cashOutId}", []);
        (await CashAsync(cashOutId)).Status.ShouldBe(CashTransactionStatus.Draft);

        HttpClient checker = await CheckerClientAsync();
        await PostAsync(checker, $"/Finance/CashTransactions/Approve/{cashOutId}", []);
        (await CashAsync(cashOutId)).Status.ShouldBe(CashTransactionStatus.Approved);
        await PostAsync(admin, $"/Finance/CashTransactions/Post/{cashOutId}", []);
        (await CashAsync(cashOutId)).Number!.ShouldStartWith("BKK/");
        await ShouldBePdfAsync(admin, $"/Finance/CashTransactions/Print/{cashOutId}");

        // A line without an account is refused by the form
        HttpResponseMessage invalid = await PostAsync(admin, "/Finance/CashTransactions/Create", new()
        {
            ["Direction"] = "Out", ["CashBankAccountId"] = bankId.ToString(), ["Date"] = Today(), ["Description"] = "No account", ["Lines[0].Amount"] = "5"
        });
        invalid.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await invalid.Content.ReadAsStringAsync()).ShouldContain("Every line needs an account");

        // Transfer bank → petty cash (same account twice is refused)
        HttpResponseMessage same = await PostAsync(admin, "/Finance/BankTransfers/Create", new()
        {
            ["FromCashBankAccountId"] = bankId.ToString(), ["ToCashBankAccountId"] = bankId.ToString(), ["Date"] = Today(), ["Amount"] = "1000"
        });
        same.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PostAsync(admin, "/Finance/BankTransfers/Create", new()
        {
            ["FromCashBankAccountId"] = bankId.ToString(), ["ToCashBankAccountId"] = pettyCashId.ToString(), ["Date"] = Today(), ["Amount"] = "50000",
            ["Reference"] = "TOPUP"
        })).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await QueryAsync(db => db.BankTransfers.CountAsync(t => t.FromCashBankAccountId == bankId))).ShouldBe(1);
        (await admin.GetStringAsync(new Uri($"/Finance/BankTransfers?branch={branchId}", UriKind.Relative))).ShouldContain("TOPUP");

        // Cash/bank book (journals are written by the outbox job, not running here) and its export
        (await admin.GetStringAsync(new Uri($"/Finance/CashBankAccounts/Ledger/{bankId}", UriKind.Relative))).ShouldContain("Opening balance");
        HttpResponseMessage book = await admin.GetAsync(new Uri(
            $"/Finance/CashBankAccounts/LedgerExport?format=xlsx&id={bankId}&from={Today()}&to={Today()}", UriKind.Relative));
        book.Content.Headers.ContentType!.MediaType.ShouldBe(ExcelExporter.ContentType);

        // Reconciliation: an unmatched statement line blocks completion; removed → completed
        Guid reconciliationId = IdOf(await PostAsync(admin, "/Finance/BankReconciliations/Create", new()
        {
            ["CashBankAccountId"] = bankId.ToString(), ["StatementDate"] = Today(), ["StatementBalance"] = "0"
        }));
        await PostAsync(admin, $"/Finance/BankReconciliations/Import/{reconciliationId}", new()
        {
            ["csv"] = $"date,description,debit,credit\n{Today()},Bank charge,6500,0\n{Today()},Interest,0,250000"
        });
        (await QueryAsync(db => db.BankReconciliations.Where(r => r.Id == reconciliationId).SelectMany(r => r.Lines).CountAsync())).ShouldBe(2);
        string details = await admin.GetStringAsync(new Uri($"/Finance/BankReconciliations/Details/{reconciliationId}", UriKind.Relative));
        details.ShouldContain("Bank charge");
        details.ShouldContain("Uncleared ledger lines");

        await PostAsync(admin, $"/Finance/BankReconciliations/Complete/{reconciliationId}", []);
        (await ReconciliationStatusAsync(reconciliationId)).ShouldBe(BankReconciliationStatus.InProgress);

        await PostAsync(admin, $"/Finance/BankReconciliations/RemoveLine/{reconciliationId}", new() { ["lineNumber"] = "1" });
        await PostAsync(admin, $"/Finance/BankReconciliations/RemoveLine/{reconciliationId}", new() { ["lineNumber"] = "2" });
        await PostAsync(admin, $"/Finance/BankReconciliations/AddLine/{reconciliationId}", new()
        {
            ["date"] = Today(), ["description"] = "Manual line", ["moneyOut"] = "1000"
        });
        (await QueryAsync(db => db.BankReconciliations.Where(r => r.Id == reconciliationId).SelectMany(r => r.Lines).CountAsync())).ShouldBe(1);
        await PostAsync(admin, $"/Finance/BankReconciliations/RemoveLine/{reconciliationId}", new() { ["lineNumber"] = "1" });
        await PostAsync(admin, $"/Finance/BankReconciliations/Complete/{reconciliationId}", []);
        (await ReconciliationStatusAsync(reconciliationId)).ShouldBe(BankReconciliationStatus.Completed);
        HttpResponseMessage statement = await admin.GetAsync(new Uri($"/Finance/BankReconciliations/StatementExport?format=pdf&id={reconciliationId}", UriKind.Relative));
        statement.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
    }

    /// <summary>
    /// Branch, vendor, feed item, warehouse, an open fiscal year and a bank account of the branch.
    /// </summary>
    private async Task<(string S, Guid BranchId, Guid CashBankId, Guid VendorId)> SetupAsync(HttpClient client)
    {
        string s = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        await PostAsync(client, "/Finance/FiscalPeriods/OpenYear", new() { ["year"] = DateTime.Today.Year.ToString(CultureInfo.InvariantCulture) });

        await PostAsync(client, "/Admin/Branches/Create", new() { ["Code"] = $"B{s}", ["Name"] = $"Branch {s}" });
        Guid branchId = await QueryAsync(db => db.Branches.Where(b => b.Code == $"B{s}").Select(b => b.Id).SingleAsync());
        await PostAsync(client, "/MasterData/Vendors/Create", new() { ["Code"] = $"V{s}", ["Name"] = $"Vendor {s}", ["PaymentTermDays"] = "30" });
        Guid vendorId = await QueryAsync(db => db.Vendors.Where(v => v.Code == $"V{s}").Select(v => v.Id).SingleAsync());
        Guid kg = await QueryAsync(db => db.Uoms.Where(u => u.Code == "KG").Select(u => u.Id).SingleAsync());
        await PostAsync(client, "/MasterData/Items/Create", new() { ["Code"] = $"F{s}", ["Name"] = $"Feed {s}", ["Category"] = "Feed", ["BaseUomId"] = kg.ToString() });
        await PostAsync(client, "/MasterData/Warehouses/Create", new() { ["Code"] = $"GA{s}", ["Name"] = "Gudang A", ["BranchId"] = branchId.ToString() });

        return (s, branchId, await CashBankAsync(client, s, branchId, "BK", "Bank"), vendorId);
    }

    private async Task<Guid> CashBankAsync(HttpClient client, string s, Guid branchId, string prefix, string type)
    {
        Guid ledgerAccountId = await AccountAsync(client, $"1-1{prefix}{s}", $"{type} {s}", "Asset");
        await PostAsync(client, "/Finance/CashBankAccounts/Create", new()
        {
            ["Code"] = $"{prefix}{s}", ["Name"] = $"{type} {s}", ["Type"] = type, ["BranchId"] = branchId.ToString(), ["AccountId"] = ledgerAccountId.ToString(),
            ["BankName"] = type == "Bank" ? "BCA" : string.Empty, ["AccountNumber"] = type == "Bank" ? "123" : string.Empty
        });

        return await QueryAsync(db => db.CashBankAccounts.Where(c => c.Code == $"{prefix}{s}").Select(c => c.Id).SingleAsync());
    }

    private async Task<Guid> AccountAsync(HttpClient client, string code, string name, string type)
    {
        await PostAsync(client, "/Finance/Accounts/Create", new() { ["Code"] = code, ["Name"] = name, ["Type"] = type, ["IsPostable"] = "true" });
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

    private async Task<VendorInvoice> InvoiceAsync(Guid id) =>
        await QueryAsync(db => db.VendorInvoices.AsNoTracking().SingleAsync(i => i.Id == id));

    private async Task<PaymentVoucherStatus> VoucherStatusAsync(Guid id) =>
        await QueryAsync(db => db.PaymentVouchers.Where(v => v.Id == id).Select(v => v.Status).SingleAsync());

    private async Task<CashTransaction> CashAsync(Guid id) =>
        await QueryAsync(db => db.CashTransactions.AsNoTracking().SingleAsync(t => t.Id == id));

    private async Task<BankReconciliationStatus> ReconciliationStatusAsync(Guid id) =>
        await QueryAsync(db => db.BankReconciliations.Where(r => r.Id == id).Select(r => r.Status).SingleAsync());

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
