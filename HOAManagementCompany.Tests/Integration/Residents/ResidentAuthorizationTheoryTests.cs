using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Residents;

/// <summary>
/// 030 T113 — only those holding <c>ManageResidents</c> in the property's own community may use the
/// Residents routes (US8-S11, US8-S12). The archived-community rows are in
/// <see cref="ArchivedCommunityWritesTheoryTests"/>.
/// </summary>
public class ResidentAuthorizationTheoryTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    public enum Route { Invite, Remove, List, Detail, Preview }

    public static TheoryData<Route> Routes => new() { Route.Invite, Route.Remove, Route.List, Route.Detail, Route.Preview };

    // US8-S11
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task AManagerOfAnotherCommunity_GetsTheSame403_AsForAPropertyOrCommunityThatDoesntExist(Route route)
    {
        var companyId = await CreateCompanyAsync();
        var a = await CreateCommunityInAsync(companyId);
        var b = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(a);
        var homeInB = await CreatePropertyAsync(b);
        var (residentInB, _) = await CreateResidentAsync(homeInB);
        using var manager = await ClientForAsync(managerEmail);

        // B's property under B; B's property under A (the manager's own community); nothing at all.
        var inB = await SendAsync(manager, route, b, homeInB, residentInB);
        var acrossCommunities = await SendAsync(manager, route, a, homeInB, residentInB);
        var missingCommunity = await SendAsync(manager, route, Guid.NewGuid(), homeInB, residentInB);
        var missingProperty = await SendAsync(manager, route, a, Guid.NewGuid(), residentInB);

        var expected = await missingCommunity.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Forbidden, missingCommunity.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(missingCommunity)).GetProperty("code").GetString());
        foreach (var res in route is Route.List ? new[] { inB } : new[] { inB, acrossCommunities, missingProperty })
        {
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal(expected, await res.Content.ReadAsStringAsync());
        }
        Assert.True(await WithDbAsync(db => db.UserProperties.AnyAsync(up => up.UserId == residentInB && up.PropertyId == homeInB)));
        Assert.False(await WithDbAsync(db => db.Invitations.AnyAsync(i => i.PropertyId == homeInB)));
    }

    // US8-S12
    [Theory]
    [InlineData(CommunityRole.BoardMember)]
    [InlineData(CommunityRole.Accountant)]
    [InlineData(CommunityRole.Resident)]
    public async Task OnlyManagersAndCompanyAdministrators_InviteAndRemove(CommunityRole role)
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync());
        await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community);
        var (janeId, _) = await CreateResidentAsync(home);
        var callerEmail = role == CommunityRole.Resident
            ? (await CreateResidentAsync(await CreatePropertyAsync(community))).Email
            : (await CreateStaffAsync(community, role)).Email;
        using var caller = await ClientForAsync(callerEmail);

        var invite = await SendAsync(caller, Route.Invite, community, home, janeId);
        var remove = await SendAsync(caller, Route.Remove, community, home, janeId);

        foreach (var res in new[] { invite, remove })
        {
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        }
        Assert.True(await WithDbAsync(db => db.UserProperties.AnyAsync(up => up.UserId == janeId && up.PropertyId == home)));
        Assert.False(await WithDbAsync(db => db.Invitations.AnyAsync(i => i.PropertyId == home)));
        Assert.Empty(Email.Sent);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, Route route, Guid community, Guid home, string userId)
    {
        var baseUrl = $"/api/v1/communities/{community}/residents";
        return route switch
        {
            Route.Invite => client.PostAsJsonAsync($"{baseUrl}/invitations", new { propertyId = home, email = "kim@example.com" }),
            Route.Remove => client.DeleteAsync($"{baseUrl}/{home}/{userId}"),
            Route.List => client.GetAsync(baseUrl),
            Route.Detail => client.GetAsync($"{baseUrl}/{home}"),
            _ => client.GetAsync($"{baseUrl}/{home}/removal-preview?userId={userId}"),
        };
    }
}
