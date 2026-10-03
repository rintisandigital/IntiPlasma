using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Domain.Access;
using Domain.Finance.Journals;
using Infrastructure.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;

namespace Web.App.IntegrationTests;

/// <summary>
/// PhaseW9: manual journals (draft → edit → maker-checker approve → post → reverse, delete draft, journal voucher PDF),
/// financial reports and their structured Excel/PDF exports, tax recap with CSV, failed events with retry, the journal
/// card on document pages and the dashboard.
/// </summary>
[Collection(nameof(WebAppCollection))]
public sealed partial class JournalsReportsTests(WebAppFactory factory)
{
    private const string CheckerPassword = "Checker123!";

    public static TheoryData<string> Pages =>
    [
        "/Finance/Journals", "/Finance/Journals/Create", "/Finance/Journals?source=Automatic&branch=all",
        "/Reports/GeneralLedger", "/Reports/TrialBalance", "/Reports/TrialBalance?zero=true&branch=all", "/Reports/IncomeStatement",
        "/Reports/BalanceSheet", "/Reports/CashFlow", "/Reports/Profitability?groupBy=Branch&branch=all", "/Reports/Tax",
        "/Reports/Tax?tab=withholding", "/Admin/FailedEvents", "/Main/Dashboard"
    ];

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Admin_Should_OpenPage(string path)
    {
        HttpClient client = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);

        (await client.GetAsync(new Uri(path, UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ManualJournal_Should_BeApprovedByChecker_Posted_AndReversed()
    {
        HttpClient admin = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        (string s, Guid branchId, Guid cashId, Guid expenseId) = await SetupAsync(admin);

        // An unbalanced journal is refused and the form is shown again
        HttpResponseMessage unbalanced = await PostAsync(admin, "/Finance/Journals/Create", JournalForm(branchId, cashId, expenseId, $"Unbalanced {s}", 100_000, 90_000));
        unbalanced.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await QueryAsync(db => db.JournalEntries.CountAsync(j => j.Description == $"Unbalanced {s}"))).ShouldBe(0);

        // Draft → edited → the maker cannot approve → the checker approves → posted with a number
        Guid journalId = IdOf(await PostAsync(admin, "/Finance/Journals/Create", JournalForm(branchId, cashId, expenseId, $"Electricity {s}", 250_000, 250_000)));
        (await JournalAsync(journalId)).Status.ShouldBe(JournalStatus.Draft);
        IdOf(await PostAsync(admin, $"/Finance/Journals/Edit/{journalId}", JournalForm(branchId, cashId, expenseId, $"Electricity {s}", 300_000, 300_000)));
        (await admin.GetStringAsync(new Uri($"/Finance/Journals/Details/{journalId}", UriKind.Relative))).ShouldContain("300.000,00");

        await PostAsync(admin, $"/Finance/Journals/Approve/{journalId}", []);
        (await JournalAsync(journalId)).Status.ShouldBe(JournalStatus.Draft);
        HttpClient checker = await CheckerClientAsync();
        await PostAsync(checker, $"/Finance/Journals/Approve/{journalId}", []);
        (await JournalAsync(journalId)).Status.ShouldBe(JournalStatus.Approved);
        await PostAsync(admin, $"/Finance/Journals/Post/{journalId}", []);
        JournalEntry posted = await JournalAsync(journalId);
        posted.Status.ShouldBe(JournalStatus.Posted);
        posted.Number.ShouldNotBeNull();

        // The general ledger of the expense account shows the posted journal with a link
        string ledger = await admin.GetStringAsync(new Uri(
            $"/Reports/GeneralLedger?accountId={expenseId}&from={Today()}&to={Today()}&branch={branchId}", UriKind.Relative));
        ledger.ShouldContain(posted.Number!);
        ledger.ShouldContain($"/Finance/Journals/Details/{journalId}");
        ledger.ShouldContain("Closing balance");

        // Journal voucher PDF and list export
        await ShouldBePdfAsync(admin, $"/Finance/Journals/Print/{journalId}");
        HttpResponseMessage list = await admin.GetAsync(new Uri($"/Finance/Journals/Export?format=xlsx&branch=all&search={s}", UriKind.Relative));
        list.Content.Headers.ContentType!.MediaType.ShouldBe(ExcelExporter.ContentType);

        // Reverse: the reason is required; the reversal journal is posted and the original becomes Reversed
        await PostAsync(admin, $"/Finance/Journals/Reverse/{journalId}", new() { ["date"] = Today() });
        (await JournalAsync(journalId)).Status.ShouldBe(JournalStatus.Posted);
        HttpResponseMessage reversed = await PostAsync(admin, $"/Finance/Journals/Reverse/{journalId}", new() { ["date"] = Today(), ["reason"] = "Wrong account" });
        Guid reversalId = IdOf(reversed);
        reversalId.ShouldNotBe(journalId);
        (await JournalAsync(journalId)).Status.ShouldBe(JournalStatus.Reversed);
        (await JournalAsync(reversalId)).Status.ShouldBe(JournalStatus.Posted);

        // Another draft is deleted
        Guid draftId = IdOf(await PostAsync(admin, "/Finance/Journals/Create", JournalForm(branchId, cashId, expenseId, $"Draft {s}", 1_000, 1_000)));
        HttpResponseMessage deleted = await PostAsync(admin, $"/Finance/Journals/Delete/{draftId}", []);
        deleted.Headers.Location!.ToString().ShouldEndWith("/Finance/Journals");
        (await QueryAsync(db => db.JournalEntries.AnyAsync(j => j.Id == draftId))).ShouldBeFalse();
    }

    [Fact]
    public async Task Reports_Should_RenderAndExport_StructuredExcelPdfAndCsv()
    {
        HttpClient admin = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        (string s, Guid branchId, Guid cashId, Guid expenseId) = await SetupAsync(admin);
        Guid journalId = IdOf(await PostAsync(admin, "/Finance/Journals/Create", JournalForm(branchId, cashId, expenseId, $"Rent {s}", 500_000, 500_000)));
        await PostAsync(await CheckerClientAsync(), $"/Finance/Journals/Approve/{journalId}", []);
        await PostAsync(admin, $"/Finance/Journals/Post/{journalId}", []);
        string period = $"from={Today()}&to={Today()}&branch={branchId}";

        string trialBalance = await admin.GetStringAsync(new Uri($"/Reports/TrialBalance?{period}", UriKind.Relative));
        trialBalance.ShouldContain($"6-9{s}");
        trialBalance.ShouldContain("The trial balance is balanced.");
        string income = await admin.GetStringAsync(new Uri($"/Reports/IncomeStatement?{period}", UriKind.Relative));
        income.ShouldContain("Total expenses");
        income.ShouldContain("Net loss");
        string balance = await admin.GetStringAsync(new Uri($"/Reports/BalanceSheet?asOf={Today()}&branch={branchId}", UriKind.Relative));
        balance.ShouldContain("Assets equal liabilities &#x2B; equity.");
        (await admin.GetStringAsync(new Uri($"/Reports/CashFlow?{period}", UriKind.Relative))).ShouldContain("Closing cash and bank");

        // Structured Excel: title block, bold total row; PDF for every report
        foreach (string report in new[] { "TrialBalance", "IncomeStatement", "CashFlow", "Profitability" })
        {
            HttpResponseMessage excel = await admin.GetAsync(new Uri($"/Reports/{report}Export?format=xlsx&{period}", UriKind.Relative));
            excel.Content.Headers.ContentType!.MediaType.ShouldBe(ExcelExporter.ContentType);
            await ShouldBePdfAsync(admin, $"/Reports/{report}Export?format=pdf&{period}");
        }

        using (var workbook = new XLWorkbook(await (await admin.GetAsync(new Uri($"/Reports/TrialBalanceExport?format=xlsx&{period}", UriKind.Relative))).Content.ReadAsStreamAsync()))
        {
            IXLWorksheet sheet = workbook.Worksheet(1);
            sheet.Cell(1, 1).GetString().ShouldBe("Trial Balance");
            IXLCell total = sheet.CellsUsed().First(c => c.GetString() == "Total");
            total.Style.Font.Bold.ShouldBeTrue();
            total.CellRight(5).GetDouble().ShouldBeGreaterThanOrEqualTo(500_000);
        }

        await ShouldBePdfAsync(admin, $"/Reports/BalanceSheetExport?format=pdf&asOf={Today()}&branch={branchId}");
        await ShouldBePdfAsync(admin, $"/Reports/GeneralLedgerExport?format=pdf&accountId={cashId}&{period}");

        // Tax recap: one worksheet per table, CSV files with the Web.Api columns
        string month = DateTime.Today.Month.ToString(CultureInfo.InvariantCulture);
        string year = DateTime.Today.Year.ToString(CultureInfo.InvariantCulture);
        using (var vat = new XLWorkbook(await (await admin.GetAsync(new Uri($"/Reports/TaxExport?format=xlsx&year={year}&month={month}&branch=all", UriKind.Relative))).Content.ReadAsStreamAsync()))
        {
            vat.Worksheets.Select(w => w.Name).ShouldBe(["Ringkasan PPN", "PPN keluaran", "Retur PPN keluaran", "PPN masukan"]);
        }

        await ShouldBePdfAsync(admin, $"/Reports/TaxExport?format=pdf&tab=withholding&year={year}&month={month}&branch=all");
        string taxPage = await admin.GetStringAsync(new Uri($"/Reports/Tax?year={year}&month={month}&branch=all", UriKind.Relative));
        taxPage.ShouldContain("CSV PPN keluaran");
        foreach ((string section, string header) in new[]
                 {
                     ("output", "nomor_invoice,tanggal,cabang"), ("output-returns", "nomor_nota_kredit"), ("input", "nomor_internal"),
                     ("withholding", "sumber,nomor_dokumen")
                 })
        {
            HttpResponseMessage csv = await admin.GetAsync(new Uri($"/Reports/TaxCsv?section={section}&year={year}&month={month}&branch=all", UriKind.Relative));
            csv.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
            (await csv.Content.ReadAsStringAsync()).ShouldContain(header);
        }
    }

    [Fact]
    public async Task FailedEvents_Should_BeListed_AndRetried()
    {
        HttpClient admin = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        var eventId = Guid.NewGuid();
        await ExecuteAsync(
            $"INSERT INTO infrastructure.outbox_messages (id, type, content, occurred_on_utc, processed_on_utc, attempts, error) " +
            $"VALUES ('{eventId}', 'Application.Tests.FakeDomainEvent', '[]', now(), now(), 5, 'FiscalPeriods.NotFoundForDate: test')");

        try
        {
            string page = await admin.GetStringAsync(new Uri("/Admin/FailedEvents", UriKind.Relative));
            page.ShouldContain("FakeDomainEvent");
            page.ShouldContain("FiscalPeriods.NotFoundForDate");
            (await admin.GetStringAsync(new Uri("/Main/Dashboard", UriKind.Relative))).ShouldContain("block closing the period");

            await PostAsync(admin, $"/Admin/FailedEvents/Retry/{eventId}", []);
            (await admin.GetStringAsync(new Uri("/Admin/FailedEvents", UriKind.Relative))).ShouldNotContain("FakeDomainEvent");
        }
        finally
        {
            await ExecuteAsync($"DELETE FROM infrastructure.outbox_messages WHERE id = '{eventId}'");
        }
    }

    [Fact]
    public async Task Dashboard_AndJournalCard_Should_FollowTheUsersRights()
    {
        HttpClient admin = await ClientAsync(WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        string dashboard = await admin.GetStringAsync(new Uri("/Main/Dashboard", UriKind.Relative));
        dashboard.ShouldContain("Cycles in production");
        dashboard.ShouldContain("Waiting for action");
        dashboard.ShouldContain("chart-trend");

        // A cash-in draft shows the journal card (empty until posted) to the admin, not to a user without Journals
        (string S, Guid BranchId, Guid CashId, Guid ExpenseId) setup = await SetupAsync(admin);
        string s = setup.S;
        Guid branchId = setup.BranchId;
        Guid cashBankId = await CashBankAsync(admin, s, branchId);
        Guid revenueId = await AccountAsync(admin, $"4-9{s}", $"Other income {s}", "Revenue");
        Guid cashInId = IdOf(await PostAsync(admin, "/Finance/CashTransactions/Create", new()
        {
            ["Direction"] = "In", ["CashBankAccountId"] = cashBankId.ToString(), ["Date"] = Today(), ["Description"] = $"Scrap sale {s}",
            ["Lines[0].AccountId"] = revenueId.ToString(), ["Lines[0].Amount"] = "75000"
        }));
        string details = await admin.GetStringAsync(new Uri($"/Finance/CashTransactions/Details/{cashInId}", UriKind.Relative));
        details.ShouldContain("journal-preview");

        Guid profileId = await factory.CreateMenuProfileAsync(
            $"Cash only {Guid.NewGuid():N}", (MenuCodes.FinanceCashTransactions, MenuRights.View));
        string email = $"cash-{Guid.NewGuid():N}@intiplasma.test";
        await factory.CreateUserAsync(email, CheckerPassword, menuAccessProfileId: profileId, branchAccessProfileId: BranchAccessProfile.AllBranchesId);
        HttpClient cashOnly = await ClientAsync(email, CheckerPassword);
        (await cashOnly.GetStringAsync(new Uri($"/Finance/CashTransactions/Details/{cashInId}", UriKind.Relative))).ShouldNotContain("journal-preview");
        string limited = await cashOnly.GetStringAsync(new Uri("/Main/Dashboard", UriKind.Relative));
        limited.ShouldNotContain("Journals to approve");
        limited.ShouldContain("Cycles in production");
    }

    private static Dictionary<string, string> JournalForm(Guid branchId, Guid cashId, Guid expenseId, string description, decimal debit, decimal credit) => new()
    {
        ["BranchId"] = branchId.ToString(), ["Date"] = Today(), ["Description"] = description,
        ["Lines[0].AccountId"] = expenseId.ToString(), ["Lines[0].Debit"] = debit.ToString(CultureInfo.InvariantCulture),
        ["Lines[1].AccountId"] = cashId.ToString(), ["Lines[1].Credit"] = credit.ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>
    /// Fiscal year open, a branch, an asset (cash) account and an expense account.
    /// </summary>
    private async Task<(string S, Guid BranchId, Guid CashId, Guid ExpenseId)> SetupAsync(HttpClient client)
    {
        string s = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        await PostAsync(client, "/Finance/FiscalPeriods/OpenYear", new() { ["year"] = DateTime.Today.Year.ToString(CultureInfo.InvariantCulture) });
        await PostAsync(client, "/Admin/Branches/Create", new() { ["Code"] = $"B{s}", ["Name"] = $"Branch {s}" });
        Guid branchId = await QueryAsync(db => db.Branches.Where(b => b.Code == $"B{s}").Select(b => b.Id).SingleAsync());

        return (s, branchId, await AccountAsync(client, $"1-9{s}", $"Cash {s}", "Asset"), await AccountAsync(client, $"6-9{s}", $"Expense {s}", "Expense"));
    }

    private async Task<Guid> AccountAsync(HttpClient client, string code, string name, string type)
    {
        await PostAsync(client, "/Finance/Accounts/Create", new() { ["Code"] = code, ["Name"] = name, ["Type"] = type, ["IsPostable"] = "true" });
        return await QueryAsync(db => db.Accounts.Where(a => a.Code == code).Select(a => a.Id).SingleAsync());
    }

    private async Task<Guid> CashBankAsync(HttpClient client, string s, Guid branchId)
    {
        Guid ledgerAccountId = await AccountAsync(client, $"1-8{s}", $"Petty cash {s}", "Asset");
        await PostAsync(client, "/Finance/CashBankAccounts/Create", new()
        {
            ["Code"] = $"KK{s}", ["Name"] = "Petty cash", ["Type"] = "PettyCash", ["BranchId"] = branchId.ToString(), ["AccountId"] = ledgerAccountId.ToString()
        });

        return await QueryAsync(db => db.CashBankAccounts.Where(c => c.Code == $"KK{s}").Select(c => c.Id).SingleAsync());
    }

    private async Task<HttpClient> CheckerClientAsync()
    {
        string email = $"checker-{Guid.NewGuid():N}@intiplasma.test";
        await factory.CreateUserAsync(email, CheckerPassword, branchAccessProfileId: BranchAccessProfile.AllBranchesId);
        return await ClientAsync(email, CheckerPassword);
    }

    private async Task<JournalEntry> JournalAsync(Guid id) =>
        await QueryAsync(db => db.JournalEntries.AsNoTracking().SingleAsync(j => j.Id == id));

    private async Task ExecuteAsync(string sql)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlRawAsync(sql);
    }

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
