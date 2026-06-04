using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Community;

public class ViolationsEventsTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task SetAuthHeaderAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!["token"]!.ToString()!);
    }

    [Fact]
    public async Task GetViolations_PropertyScoped_Returns200()
    {
        await SetAuthHeaderAsync();
        var response = await Client.GetAsync("/api/v1/community/violations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_Returns200()
    {
        await SetAuthHeaderAsync();
        var response = await Client.GetAsync("/api/v1/community/events");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RsvpEvent_UnknownEvent_Returns404()
    {
        await SetAuthHeaderAsync();
        var response = await Client.PostAsJsonAsync($"/api/v1/community/events/{Guid.NewGuid()}/rsvp", new { attending = true });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
