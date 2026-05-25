using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Community;

public class DocumentTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static readonly Guid CcrDocumentId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003");

    private async Task SetAuthHeaderAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!["token"]!.ToString()!);
    }

    [Fact]
    public async Task GetDocuments_Returns200WithPinnedFirst()
    {
        await SetAuthHeaderAsync();
        var response = await Client.GetAsync("/api/v1/community/documents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDocumentDownload_UnknownId_Returns404()
    {
        await SetAuthHeaderAsync();
        var response = await Client.GetAsync($"/api/v1/community/documents/{Guid.NewGuid()}/download");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDocumentDownload_KnownDocument_ReturnsPresignedUrl()
    {
        await EnsureTestDocumentsInStorageAsync();
        await SetAuthHeaderAsync();

        var response = await Client.GetAsync($"/api/v1/community/documents/{CcrDocumentId}/download");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = body.GetProperty("url").GetString();
        var expiresAt = body.GetProperty("expiresAt").GetString();

        Assert.False(string.IsNullOrWhiteSpace(url));
        Assert.False(string.IsNullOrWhiteSpace(expiresAt));
        Assert.Contains("hoa-documents", url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ccr-declaration.pdf", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetDocumentDownload_PresignedUrl_ReturnsPdfFromMinio()
    {
        await EnsureTestDocumentsInStorageAsync();
        await SetAuthHeaderAsync();

        var response = await Client.GetAsync($"/api/v1/community/documents/{CcrDocumentId}/download");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var presignedUrl = body.GetProperty("url").GetString()!;

        // Testcontainers MinIO is HTTP-only; rewrite host to mapped localhost if needed
        var fetchUrl = presignedUrl;
        if (fetchUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            fetchUrl = "http://" + fetchUrl["https://".Length..];
        var minioHost = new Uri(Fixture.MinioEndpoint).Authority;
        var urlHost = new Uri(fetchUrl).Authority;
        if (urlHost != minioHost)
            fetchUrl = fetchUrl.Replace(urlHost, minioHost);

        // Simulate a browser fetching the object directly from MinIO via the presigned URL
        using var fileClient = new HttpClient();
        var fileResponse = await fileClient.GetAsync(fetchUrl);
        Assert.Equal(HttpStatusCode.OK, fileResponse.StatusCode);

        var content = await fileResponse.Content.ReadAsStringAsync();
        Assert.StartsWith("%PDF", content);
    }
}
