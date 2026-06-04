using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Property;

public class DirectoryAddressTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task SetAuthHeaderAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!["token"]!.ToString()!);
    }

    [Fact]
    public async Task GetAddressHistory_Returns200()
    {
        await SetAuthHeaderAsync();
        var response = await Client.GetAsync("/api/v1/property/address-history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDirectoryFields_Returns200()
    {
        await SetAuthHeaderAsync();
        var response = await Client.GetAsync("/api/v1/property/directory-fields");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatchDirectoryField_KnownKey_Returns200()
    {
        await SetAuthHeaderAsync();
        var response = await Client.PatchAsJsonAsync("/api/v1/property/directory-fields/phone", new { shared = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatchDirectoryField_UnknownKey_Returns404()
    {
        await SetAuthHeaderAsync();
        var response = await Client.PatchAsJsonAsync("/api/v1/property/directory-fields/unknownfield", new { shared = true });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
