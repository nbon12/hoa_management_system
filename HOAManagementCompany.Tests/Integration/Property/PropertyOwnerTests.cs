using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Property;

public class PropertyOwnerTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task SetAuthHeaderAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!["token"]!.ToString()!);
    }

    [Fact]
    public async Task GetProperty_HappyPath_Returns200()
    {
        await SetAuthHeaderAsync();
        var response = await Client.GetAsync("/api/v1/property");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.True(body!.ContainsKey("accountNumber"));
    }

    [Fact]
    public async Task GetOwner_HappyPath_Returns200()
    {
        await SetAuthHeaderAsync();
        var response = await Client.GetAsync("/api/v1/property/owner");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatchOwner_ValidRequest_Returns200()
    {
        await SetAuthHeaderAsync();
        var response = await Client.PatchAsJsonAsync("/api/v1/property/owner", new { phone = "408-555-9999" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatchOwner_InvalidEmail_Returns422()
    {
        await SetAuthHeaderAsync();
        var response = await Client.PatchAsJsonAsync("/api/v1/property/owner", new { email = "not-an-email" });
        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task GetProperty_Unauthenticated_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/property");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
