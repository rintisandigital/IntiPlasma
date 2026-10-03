using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Infrastructure.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;

namespace Web.App.IntegrationTests;

/// <summary>
/// PhaseW3: finance setup pages (chart of accounts, cost centers, fiscal periods, journal templates, auto journal
/// mappings, cash/bank accounts).
/// </summary>
[Collection(nameof(WebAppCollection))]
public sealed partial class FinanceSetupTests(WebAppFactory factory)
{
    public static TheoryData<string> Pages =>
    [
        "/Finance/Accounts", "/Finance/Accounts/Create",
        "/Finance/CostCenters", "/Finance/CostCenters/Create",
        "/Finance/FiscalPeriods",
        "/Finance/JournalTemplates", "/Finance/JournalTemplates/Create",
        "/Finance/JournalMappings", "/Finance/JournalMappings/Create?eventType=SalesInvoice",
        "/Finance/JournalMappings/Create?eventType=SalesInvoice&fromDefault=true",
        "/Finance/CashBankAccounts", "/Finance/CashBankAccounts/Create"
    ];

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Admin_Should_OpenPage(string path)
    {
        HttpClient client = await AdminClientAsync();

        HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sidebar_Should_ShowFinanceSetupMenus()
    {
        HttpClient client = await AdminClientAsync();

        string mainboard = await client.GetStringAsync(new Uri("/Main", UriKind.Relative));

        mainboard.ShouldContain($"data-menu-code=\"{MenuCodes.FinanceAccounts}\"");
        mainboard.ShouldContain($"data-menu-code=\"{MenuCodes.FinanceCashBankAccounts}\"");
        mainboard.ShouldContain($"data-menu-code=\"{MenuCodes.FinanceJournals}\""); // released in W9
    }

    [Fact]
    public async Task ChartOfAccounts_Should_ListSeededTree_AndExport()
    {
        HttpClient client = await AdminClientAsync();

        string list = await client.GetStringAsync(new Uri("/Finance/Accounts", UriKind.Relative));
        list.ShouldContain("1-1201");
        list.ShouldContain("Contra"); // accumulated depreciation

        HttpResponseMessage export = await client.GetAsync(new Uri("/Finance/Accounts/Export?format=xlsx", UriKind.Relative));
        export.StatusCode.ShouldBe(HttpStatusCode.OK);
        export.Content.Headers.ContentType!.MediaType.ShouldBe(ExcelExporter.ContentType);
        export.Content.Headers.ContentDisposition!.FileName!.ShouldContain("chart-of-accounts_");
    }

    [Fact]
    public async Task CostCenter_Should_BeCreated_ThenDeactivated()
    {
        HttpClient client = await AdminClientAsync();
        string code = $"CC{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        await PostFormAsync(client, "/Finance/CostCenters/Create", new() { ["Code"] = code, ["Name"] = "Area Utara" });
        Guid id = await QueryAsync(db => db.CostCenters.Where(c => c.Code == code).Select(c => c.Id).SingleAsync());

        // An unchecked "Active" switch posts nothing: the record must become inactive.
        HttpResponseMessage saved = await PostFormAsync(client, $"/Finance/CostCenters/Edit/{id}",
            new() { ["Id"] = id.ToString(), ["Code"] = code, ["Name"] = "Area Utara" });

        saved.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await QueryAsync(db => db.CostCenters.Where(c => c.Id == id).Select(c => c.IsActive).SingleAsync())).ShouldBeFalse();
    }

    [Fact]
    public async Task MasterRecord_Should_BeDeactivated_WhenActiveSwitchIsOff()
    {
        HttpClient client = await AdminClientAsync();
        string code = $"C{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        await PostFormAsync(client, "/MasterData/Customers/Create",
            new() { ["Code"] = code, ["Name"] = "Bakul", ["PaymentTermDays"] = "7", ["CreditLimit"] = "1000000" });
        Guid id = await QueryAsync(db => db.Customers.Where(c => c.Code == code).Select(c => c.Id).SingleAsync());

        await PostFormAsync(client, $"/MasterData/Customers/Edit/{id}",
            new() { ["Id"] = id.ToString(), ["Code"] = code, ["Name"] = "Bakul", ["PaymentTermDays"] = "7", ["CreditLimit"] = "1000000" });

        (await QueryAsync(db => db.Customers.Where(c => c.Id == id).Select(c => c.IsActive).SingleAsync())).ShouldBeFalse();
    }

    [Fact]
    public async Task FiscalYear_Should_Open_AndShowChecklist()
    {
        HttpClient client = await AdminClientAsync();
        const int year = 2099;

        HttpResponseMessage opened = await PostFormAsync(client, "/Finance/FiscalPeriods/OpenYear", new() { ["year"] = "2099" });

        opened.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        Guid january = await QueryAsync(db => db.FiscalPeriods.Where(p => p.Year == year && p.Month == 1).Select(p => p.Id).SingleAsync());
        string checklist = await client.GetStringAsync(new Uri($"/Finance/FiscalPeriods/Details/{january}", UriKind.Relative));
        checklist.ShouldContain($"January {year}");
        checklist.ShouldContain("Manual journals still draft/approved");
    }

    [Fact]
    public async Task AccountLookup_Should_ReturnPostableAccounts()
    {
        HttpClient client = await AdminClientAsync();

        string json = await client.GetStringAsync(new Uri("/Lookup/Accounts?q=Bank&type=Asset", UriKind.Relative));

        using var document = JsonDocument.Parse(json);
        string[] labels = [.. document.RootElement.EnumerateArray().Select(e => e.GetProperty("text").GetString()!)];
        labels.ShouldContain(l => l.StartsWith("1-1201", StringComparison.Ordinal));
        labels.ShouldNotContain(l => l.StartsWith("1-1200 ", StringComparison.Ordinal)); // header account
    }

    private async Task<T> QueryAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string path, Dictionary<string, string> fields)
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
