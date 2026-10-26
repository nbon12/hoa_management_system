using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.RateLimiting;

/// <summary>
/// 030 T153 / constitution §7 — the feature's writes use the per-user <c>board-writes</c> policy,
/// and the anonymous invitation routes use the per-client <c>auth</c> policy.
/// </summary>
public class CompanyAdministrationRateLimitTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private const int Limit = 2;

    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() => new Dictionary<string, string?>
    {
        ["RateLimiting:BoardWritesPermitsPerMinute"] = Limit.ToString(),
        ["RateLimiting:AuthPermitsPerMinute"] = Limit.ToString(),
        ["RateLimiting:UnknownPermitsPerMinute"] = Limit.ToString(),
    };

    public static TheoryData<string> Writes => new() { "community create", "profile PATCH", "membership create", "resident invite" };

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task ABurstOverTheBoardWritesLimit_Is429(string write)
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync());
        var (managerId, _) = await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community);
        // A minted token: signing in would spend the auth limit this class also lowers.
        using var manager = ClientWithToken(MintToken(managerId));

        Task<HttpResponseMessage> Send() => write switch
        {
            "community create" => manager.PostAsJsonAsync("/api/v1/communities", new
            {
                legalName = "Keystone Glen HOA, Inc.", communityName = $"Keystone Glen {Guid.NewGuid():N}"[..22], county = "Wake",
                formationDate = "2004-03-14", managementStartDate = "2026-11-01",
            }),
            "profile PATCH" => manager.PatchAsJsonAsync($"/api/v1/communities/{community}/profile", new { county = $"County {Guid.NewGuid():N}"[..12] }),
            "membership create" => manager.PostAsJsonAsync($"/api/v1/communities/{community}/memberships",
                new { role = (int)CommunityRole.BoardMember, email = $"lee-{Guid.NewGuid():N}@example.com" }),
            _ => manager.PostAsJsonAsync($"/api/v1/communities/{community}/residents/invitations",
                new { propertyId = home, email = $"kim-{Guid.NewGuid():N}@example.com" }),
        };

        for (var i = 0; i < Limit; i++)
        {
            var res = await Send();
            Assert.True(res.IsSuccessStatusCode, $"{write} #{i + 1}: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Send()).StatusCode);
    }

    [Fact]
    public async Task ABurstOverTheAuthLimit_OnTheInvitationPreview_Is429()
    {
        using var anonymous = CreateClient();

        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i <= Limit; i++)
            responses.Add(await anonymous.PostAsJsonAsync("/api/v1/invitations/preview", new { token = new string('a', 43) }));

        Assert.All(responses.Take(Limit), r => Assert.NotEqual(HttpStatusCode.TooManyRequests, r.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, responses[^1].StatusCode);
    }
}
