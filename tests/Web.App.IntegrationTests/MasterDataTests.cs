using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Domain.Access;
using Microsoft.AspNetCore.Mvc.Testing;
using Web.App.Infrastructure.Authorization;
using Web.App.Infrastructure.Export;

namespace Web.App.IntegrationTests;

/// <summary>
/// PhaseW2: master data and partnership pages, exports (Export right) and attachments.
/// </summary>
[Collection(nameof(WebAppCollection))]
public sealed partial class MasterDataTests(WebAppFactory factory)
{
    private const string Password = "Password123";

    public static TheoryData<string> Pages =>
    [
        "/MasterData/Uoms", "/MasterData/Uoms/Create",
        "/MasterData/TaxCodes", "/MasterData/TaxCodes/Create",
        "/MasterData/Items", "/MasterData/Items/Create",
        "/MasterData/Warehouses", "/MasterData/Warehouses/Create",
        "/MasterData/Vendors", "/MasterData/Vendors/Create",
        "/MasterData/Customers", "/MasterData/Customers/Create",
        "/Partnership/Farmers", "/Partnership/Farmers/Create",
        "/Partnership/Coops", "/Partnership/Coops/Create",
        "/Partnership/Contracts", "/Partnership/Contracts/Create"
    ];

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Admin_Should_OpenPage(string path)
    {
        HttpClient client = await AdminClientAsync();

        HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("xlsx", ExcelExporter.ContentType, "PK")]
    [InlineData("pdf", PdfListExporter.ContentType, "%PDF-")]
    public async Task Export_Should_ReturnFile(string format, string contentType, string signature)
    {
        HttpClient client = await AdminClientAsync();

        HttpResponseMessage response = await client.GetAsync(new Uri($"/MasterData/Uoms/Export?format={format}", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(contentType);
        response.Content.Headers.ContentDisposition!.FileName!.ShouldContain("uoms_");
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, signature.Length).ShouldBe(signature);
    }

    [Fact]
    public async Task Export_WithoutExportRight_Should_BeForbidden()
    {
        Guid profileId = await factory.CreateMenuProfileAsync(
            $"UoM viewer {Guid.NewGuid():N}", (MenuCodes.MasterUoms, MenuRights.View));
        string email = $"viewer-{Guid.NewGuid():N}@intiplasma.test";
        await factory.CreateUserAsync(email, Password, menuAccessProfileId: profileId);
        HttpClient client = CreateClient();
        await LoginAsync(client, email, Password);

        HttpResponseMessage list = await client.GetAsync(new Uri("/MasterData/Uoms", UriKind.Relative));
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        string html = await list.Content.ReadAsStringAsync();
        html.ShouldNotContain("format=xlsx"); // no export buttons
        html.ShouldNotContain("Add Unit");

        HttpResponseMessage export = await client.GetAsync(new Uri("/MasterData/Uoms/Export?format=xlsx", UriKind.Relative));
        export.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        export.Headers.Location!.ToString().ShouldContain("/Error/403");
    }

    [Fact]
    public async Task Admin_Should_CreateUom_AndFindItInTheList()
    {
        HttpClient client = await AdminClientAsync();
        string code = $"U{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        string form = await client.GetStringAsync(new Uri("/MasterData/Uoms/Create", UriKind.Relative));
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Code"] = code,
            ["Name"] = "Test unit",
            ["__RequestVerificationToken"] = AntiforgeryToken(form)
        });
        HttpResponseMessage created = await client.PostAsync(new Uri("/MasterData/Uoms/Create", UriKind.Relative), content);

        created.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        string list = await client.GetStringAsync(new Uri($"/MasterData/Uoms?search={code}", UriKind.Relative));
        list.ShouldContain(code);
    }

    [Fact]
    public async Task Attachment_Should_Upload_ThenOpen()
    {
        HttpClient client = await AdminClientAsync();
        string page = await client.GetStringAsync(new Uri("/MasterData/Vendors/Create", UriKind.Relative));
        byte[] pdf = "%PDF-1.4\n1 0 obj << >> endobj\ntrailer << >>\n%%EOF\n"u8.ToArray();

        using var upload = new MultipartFormDataContent();
        using var file = new ByteArrayContent(pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        upload.Add(file, "file", "quotation.pdf");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/Attachments/Upload", UriKind.Relative)) { Content = upload };
        request.Headers.Add("X-CSRF-TOKEN", AntiforgeryToken(page));

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("fileName").GetString().ShouldBe("quotation.pdf");
        string id = json.RootElement.GetProperty("id").GetString()!;

        HttpResponseMessage opened = await client.GetAsync(new Uri($"/Attachments/File/{id}", UriKind.Relative));
        opened.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await opened.Content.ReadAsByteArrayAsync()).ShouldBe(pdf);
    }

    [Fact]
    public async Task Lookup_Should_ReturnJson()
    {
        HttpClient client = await AdminClientAsync();

        HttpResponseMessage response = await client.GetAsync(new Uri("/Lookup/Items?q=", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        HttpClient client = CreateClient();
        await LoginAsync(client, WebAppFactory.AdminEmail, WebAppFactory.AdminPassword);
        return client;
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        string page = await client.GetStringAsync(new Uri("/Auth/Login", UriKind.Relative));

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        });

        HttpResponseMessage response = await client.PostAsync(new Uri("/Auth/Login", UriKind.Relative), form);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    private static string AntiforgeryToken(string html) => TokenRegex().Match(html).Groups[1].Value;

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
