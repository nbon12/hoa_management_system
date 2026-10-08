using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Property.Architectural;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Dashboard;

/// <summary>
/// 029 T047 / FR-017 — the dashboard alert counts only the ACTIVE property's open architectural requests that
/// have an unanswered board question, and links to the oldest one.
/// </summary>
public class DashboardArchitecturalAlertTests(TestDatabaseFixture fixture) : ResidentArcTestBase(fixture)
{
    private static async Task<JsonElement> AlertAsync(HttpClient http) =>
        (await http.GetFromJsonAsync<JsonElement>("/api/v1/dashboard")).GetProperty("architecturalInfoRequested");

    [Fact]
    public async Task NoArchitecturalRequests_CountIsZero()
    {
        var r = await CreateResidentAsync();

        var alert = await AlertAsync(r.Http);

        Assert.Equal(0, alert.GetProperty("count").GetInt32());
        Assert.Equal(JsonValueKind.Null, alert.GetProperty("applicationId").ValueKind);
    }

    [Fact]
    public async Task CountsOnlyOpenRequestsWithUnansweredQuestions_OnTheActiveProperty()
    {
        var r = await CreateResidentAsync();
        var neighbour = await CreateResidentAsync(r.CommunityId);
        var asker = await CreateMemberAsync(r.CommunityId, CommunityRole.BoardMember);
        var older = await SeedApplicationAsync(r, new AppSpec());
        var newer = await SeedApplicationAsync(r, new AppSpec());
        var answered = await SeedApplicationAsync(r, new AppSpec());
        var closed = await SeedApplicationAsync(r, new AppSpec { Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Approved });
        var neighbours = await SeedApplicationAsync(neighbour, new AppSpec());
        await SeedInfoRequestAsync(newer, asker.UserId, "q", at: Clock.UtcNow.AddHours(-1));
        await SeedInfoRequestAsync(older, asker.UserId, "q", at: Clock.UtcNow.AddHours(-5));
        await SeedInfoRequestAsync(answered, asker.UserId, "q", respondedAt: Clock.UtcNow);
        await SeedInfoRequestAsync(closed, asker.UserId, "q");
        await SeedInfoRequestAsync(neighbours, asker.UserId, "q");

        var alert = await AlertAsync(r.Http);

        Assert.Equal(2, alert.GetProperty("count").GetInt32());
        Assert.Equal(older, alert.GetProperty("applicationId").GetGuid());
    }
}
