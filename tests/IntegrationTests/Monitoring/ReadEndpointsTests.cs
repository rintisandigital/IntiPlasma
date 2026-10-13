using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Monitoring;

/// <summary>
/// Audit 2026-10-04: read endpoints that Web.App already had (dashboard, audit log, customer credit, settleable cycles,
/// journals per document, credit note and bank transfer detail).
/// </summary>
public sealed class ReadEndpointsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Admin_Should_ReadTheNewEndpoints()
    {
        await AuthenticateAsAdminAsync();

        (await HttpClient.GetAsync(new Uri("dashboard?asOf=2026-10-13", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await HttpClient.GetAsync(new Uri("audit-logs?pageSize=5", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await HttpClient.GetAsync(new Uri("costing/settleable-cycles", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await HttpClient.GetAsync(new Uri($"finance/journals/by-source?sourceIds={Guid.NewGuid()}", UriKind.Relative)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await HttpClient.GetAsync(new Uri($"sales/credit-notes/{Guid.NewGuid()}", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await HttpClient.GetAsync(new Uri($"finance/bank-transfers/{Guid.NewGuid()}", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CustomerCredit_Should_ShowTheLimit_AndWhatIsAvailable()
    {
        await AuthenticateAsAdminAsync();
        string code = $"C{Guid.NewGuid():N}"[..10];

        HttpResponseMessage created = await HttpClient.PostAsJsonAsync("customers", new
        {
            code,
            name = "Credit Test",
            taxIdentity = new { npwp = (string?)null, nitku = (string?)null, isPkp = false },
            paymentTermDays = 14,
            creditLimit = 25_000_000m
        });
        created.EnsureSuccessStatusCode();
        Guid customerId = (await created.Content.ReadFromJsonAsync<Guid>())!;

        CreditResponse? credit = await HttpClient.GetFromJsonAsync<CreditResponse>(new Uri($"customers/{customerId}/credit", UriKind.Relative));

        credit!.CreditLimit.ShouldBe(25_000_000m);
        credit.Exposure.ShouldBe(0m);
        credit.Available.ShouldBe(25_000_000m);
    }

    [Fact]
    public async Task UserWithoutPermission_Should_NotSeeTheDashboard_OrTheAuditLog()
    {
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        (await HttpClient.GetAsync(new Uri("dashboard?asOf=2026-10-13", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await HttpClient.GetAsync(new Uri("audit-logs", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private sealed record CreditResponse(Guid CustomerId, decimal CreditLimit, decimal Exposure, decimal Available);
}
