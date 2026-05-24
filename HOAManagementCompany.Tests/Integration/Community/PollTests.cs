using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Community;

public class PollTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task SetAuthHeaderAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!["token"]!.ToString()!);
    }

    [Fact]
    public async Task GetPoll_ReturnsActivePoll()
    {
        await SetAuthHeaderAsync();
        var response = await Client.GetAsync("/api/v1/community/poll");
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task VotePoll_InvalidOptionIndex_Returns422()
    {
        await SetAuthHeaderAsync();
        var pollRes = await Client.GetAsync("/api/v1/community/poll");
        if (pollRes.StatusCode != HttpStatusCode.OK) return;

        var pollBody = await pollRes.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var pollId = pollBody!["id"]!.ToString();

        var voteRes = await Client.PostAsJsonAsync($"/api/v1/community/poll/{pollId}/vote", new { optionIndex = -1 });
        Assert.Equal((HttpStatusCode)422, voteRes.StatusCode);
    }
}
