using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IntegrationTests.Documents;

public sealed class AttachmentsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x02, 0x03];

    private sealed record AttachmentResponse(Guid Id, string FileName, string ContentType, string Status, Guid? BranchId);

    private sealed record FarmerResponse(Guid Id, Guid[] Documents);

    private async Task<AttachmentResponse> UploadAsync(byte[] content, string fileName)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);

        HttpResponseMessage response = await HttpClient.PostAsync("attachments", form);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<AttachmentResponse>())!;
    }

    private async Task<Guid> CreateFarmerAsync(Guid[] documents)
    {
        string suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        HttpResponseMessage branch = await HttpClient.PostAsJsonAsync("branches", new { code = $"B{suffix}", name = "Cabang Uji" });
        branch.EnsureSuccessStatusCode();
        Guid branchId = await branch.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage farmer = await HttpClient.PostAsJsonAsync("farmers", new
        {
            code = $"F{suffix}",
            name = "Farm Inti",
            type = "Inti",
            branchId,
            taxIdentity = new { isPkp = false },
            bankAccount = new { },
            documents
        });
        farmer.EnsureSuccessStatusCode();

        return await farmer.Content.ReadFromJsonAsync<Guid>();
    }

    [Fact]
    public async Task Upload_Should_RejectUnsupportedFiles()
    {
        await AuthenticateAsAdminAsync();

        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent("MZ not an image"u8.ToArray());
        form.Add(file, "file", "virus.jpg");

        HttpResponseMessage response = await HttpClient.PostAsync("attachments", form);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Attachment_Should_FollowItsOwner_FromUploadToDelete()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        AttachmentResponse uploaded = await UploadAsync(Jpeg, "ktp.jpg");
        uploaded.ContentType.ShouldBe("image/jpeg");
        uploaded.Status.ShouldBe("Temporary");

        // Act: attach on create
        Guid farmerId = await CreateFarmerAsync([uploaded.Id]);

        // Assert
        FarmerResponse? farmer = await HttpClient.GetFromJsonAsync<FarmerResponse>($"farmers/{farmerId}");
        farmer!.Documents.ShouldBe([uploaded.Id]);

        AttachmentResponse? linked = await HttpClient.GetFromJsonAsync<AttachmentResponse>($"attachments/{uploaded.Id}");
        linked!.Status.ShouldBe("Linked");
        linked.BranchId.ShouldNotBeNull();

        HttpResponseMessage content = await HttpClient.GetAsync($"attachments/{uploaded.Id}/content");
        content.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");
        (await content.Content.ReadAsByteArrayAsync()).ShouldBe(Jpeg);

        (await HttpClient.DeleteAsync($"attachments/{uploaded.Id}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Act: detach, then delete
        HttpResponseMessage detached = await HttpClient.PutAsJsonAsync($"farmers/{farmerId}/documents", new { documents = Array.Empty<Guid>() });
        detached.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await HttpClient.DeleteAsync($"attachments/{uploaded.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await HttpClient.GetAsync($"attachments/{uploaded.Id}/content")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateWithUnknownAttachment_Should_ReturnProblem()
    {
        await AuthenticateAsAdminAsync();

        string suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        HttpResponseMessage branch = await HttpClient.PostAsJsonAsync("branches", new { code = $"B{suffix}", name = "Cabang Uji" });
        Guid branchId = await branch.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("farmers", new
        {
            code = $"F{suffix}",
            name = "Farm Inti",
            type = "Inti",
            branchId,
            taxIdentity = new { isPkp = false },
            bankAccount = new { },
            documents = new[] { Guid.NewGuid() }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
