using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Community;

public class AnnouncementTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task SetAuthHeaderAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!["token"]!.ToString()!);
    }

    [Fact]
    public async Task GetAnnouncements_HappyPath_ReturnsPaginatedList()
    {
        await SetAuthHeaderAsync();
        var response = await Client.GetAsync("/api/v1/community/announcements");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.True(body!.ContainsKey("items"));
    }

    [Fact]
    public async Task GetAnnouncement_UnknownId_Returns404()
    {
        await SetAuthHeaderAsync();
        var response = await Client.GetAsync($"/api/v1/community/announcements/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAnnouncements_Unauthenticated_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/community/announcements");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
