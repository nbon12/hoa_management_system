using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Property.Architectural;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.RateLimiting;

/// <summary>
/// 029 T068 / constitution §7 — resident architectural writes are rate-limited per user by the
/// <c>resident-writes</c> policy. Reads are not limited, and one resident can't throttle another.
/// </summary>
public class ResidentWritesRateLimitTests(TestDatabaseFixture fixture) : ResidentArcTestBase(fixture)
{
    private const int Limit = 2;

    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() => new Dictionary<string, string?>
    {
        ["RateLimiting:ResidentWritesPermitsPerMinute"] = Limit.ToString(),
    };

    private static Task<HttpResponseMessage> CreateDraft(HttpClient http) =>
        http.PostAsJsonAsync($"{Base}/drafts", new Dictionary<string, object?> { ["projectType"] = "Fence" });

    [Fact]
    public async Task ThirdWriteInTheWindow_Is429_OtherResidentsAndReadsUnaffected()
    {
        var a = await CreateResidentAsync();
        var b = await CreateResidentAsync(a.CommunityId);

        for (var i = 0; i < Limit; i++)
            Assert.Equal(HttpStatusCode.Created, (await CreateDraft(a.Http)).StatusCode);
        var throttled = await CreateDraft(a.Http);
        var otherResident = await CreateDraft(b.Http);
        var reads = await Task.WhenAll(Enumerable.Range(0, Limit + 2).Select(_ => a.Http.GetAsync(Base)));

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.Equal(HttpStatusCode.Created, otherResident.StatusCode);
        Assert.All(reads, res => Assert.Equal(HttpStatusCode.OK, res.StatusCode));
    }
}
