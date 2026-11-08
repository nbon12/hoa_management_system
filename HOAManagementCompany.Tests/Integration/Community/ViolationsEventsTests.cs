using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

    // 003 FR-034: `category` is repeatable; repeated values return the union, a single value still filters.
    [Theory]
    [InlineData("category=Board&category=Amenity", new[] { "Board", "Amenity" })]
    [InlineData("category=Board", new[] { "Board" })]
    public async Task GetEvents_CategoryFilter_ReturnsTheUnionOfTheGivenCategories(string query, string[] expected)
    {
        await SetAuthHeaderAsync();
        var response = await Client.GetAsync($"/api/v1/community/events?{query}&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var categories = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("category").GetString()!).ToList();
        Assert.All(categories, c => Assert.Contains(c, expected));
        Assert.All(expected, c => Assert.Contains(c, categories));
    }

    [Fact]
    public async Task RsvpEvent_UnknownEvent_Returns404()
    {
        await SetAuthHeaderAsync();
        var response = await Client.PostAsJsonAsync($"/api/v1/community/events/{Guid.NewGuid()}/rsvp", new { attending = true });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
