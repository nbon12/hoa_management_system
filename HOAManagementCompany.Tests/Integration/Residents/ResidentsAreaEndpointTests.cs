using System.Net;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Residents;

/// <summary>
/// 030 T112 — the Residents area: the property list and the property drawer (US8-S13, S14, S18;
/// FR-024d; contracts/community-settings.md § GET …/residents).
/// </summary>
public class ResidentsAreaEndpointTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private const string Street = "711 Keystone Park Dr";

    // US8-S13
    [Fact]
    public async Task TheList_PagesSearchesAndFilters_TheCommunitysProperties()
    {
        var (community, homes, managerId, manager) = await CommunityWith120HomesAsync();

        var first = await ListAsync(manager, community, "");
        Assert.Equal(120, first.GetProperty("total").GetInt32());
        Assert.True(first.GetProperty("propertiesLoaded").GetBoolean());
        var items = first.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(25, items.Count);
        Assert.Equal(Enumerable.Range(1, 25).Select(n => $"{Street} #{n}"), items.Select(i => i.GetProperty("address").GetString()));
        var nine = items[8];
        Assert.Equal([$"Resident 9"], nine.GetProperty("residentNames").EnumerateArray().Select(n => n.GetString()));
        Assert.Equal(JsonValueKind.Null, nine.GetProperty("matchedEmail").ValueKind);
        Assert.Equal(0, nine.GetProperty("invitationSummary").GetProperty("pending").GetInt32());
        var counts = first.GetProperty("counts");
        Assert.Equal((120, 7, 2, 1), (counts.GetProperty("all").GetInt32(), counts.GetProperty("noResident").GetInt32(),
            counts.GetProperty("invitationPending").GetInt32(), counts.GetProperty("invitationExpired").GetInt32()));

        // Natural order: #9 comes before #10.
        Assert.True(items.FindIndex(i => i.GetProperty("address").GetString() == $"{Street} #9")
                    < items.FindIndex(i => i.GetProperty("address").GetString() == $"{Street} #10"));

        Assert.Equal([homes[29]], Ids(await ListAsync(manager, community, "?search=keystone%20park%20dr%20%2329")));
        Assert.Equal(homes.Where(h => h.Key > 113).Select(h => h.Value).Order(),
            Ids(await ListAsync(manager, community, "?filter=noResident")).Order());
        // Pending includes Scheduled.
        Assert.Equal(new[] { homes[40], homes[41] }.Order(), Ids(await ListAsync(manager, community, "?filter=invitationPending")).Order());
        Assert.Equal([homes[42]], Ids(await ListAsync(manager, community, "?filter=invitationExpired")));

        var byEmail = await ListAsync(manager, community, "?search=resident-77%40");
        var matched = Assert.Single(byEmail.GetProperty("items").EnumerateArray());
        Assert.Equal((homes[77], "resident-77@example.com"),
            (matched.GetProperty("propertyId").GetGuid(), matched.GetProperty("matchedEmail").GetString()));

        var clamped = await ListAsync(manager, community, "?limit=101");
        Assert.Equal((100, 100), (clamped.GetProperty("limit").GetInt32(), clamped.GetProperty("items").GetArrayLength()));

        // US8-S18: one access event per list load or search.
        Assert.Equal(7, AccessEvents(managerId, community, "communities/residents").Count);
    }

    // US8-S13 (the drawer)
    [Fact]
    public async Task TheDrawer_ListsResidents_AndEveryInvitationWithWhoSentIt()
    {
        var (community, homes, managerId, manager) = await CommunityWith120HomesAsync();
        var home = homes[40];
        await InsertInvitationAsync(community, home, "revoked@example.com", managerId, InvitationState.Revoked);
        await InsertInvitationAsync(community, home, "expired@example.com", managerId, InvitationState.Pending, expired: true);

        var res = await manager.GetAsync($"/api/v1/communities/{community}/residents/{home}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal($"{Street} #40", body.GetProperty("address").GetString());
        var resident = Assert.Single(body.GetProperty("residents").EnumerateArray());
        Assert.Equal(("Resident 40", "resident-40@example.com", "2025-06-01"),
            (resident.GetProperty("name").GetString(), resident.GetProperty("email").GetString(), resident.GetProperty("residentSince").GetString()));
        var invitations = body.GetProperty("invitations").EnumerateArray()
            .Select(i => (i.GetProperty("email").GetString(), i.GetProperty("effectiveState").GetString(), i.GetProperty("sentBy").GetString()))
            .OrderBy(i => i.Item1).ToList();
        Assert.Equal([("expired@example.com", "Expired", "Board Tester"), ("revoked@example.com", "Revoked", "Board Tester"),
            ("scheduled@example.com", "Scheduled", "Board Tester")], invitations);
        Assert.True(body.GetProperty("canInvite").GetBoolean());
        Assert.Equal("America/New_York", body.GetProperty("timeZoneId").GetString());
        // US8-S18
        Assert.Single(AccessEvents(managerId, community, "communities/residents/property"));
    }

    // US8-S14
    [Fact]
    public async Task ACommunityWithNoProperties_SaysSo()
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync());
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);

        var body = await ListAsync(manager, community, "");

        Assert.False(body.GetProperty("propertiesLoaded").GetBoolean());
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
        Assert.Equal(0, body.GetProperty("total").GetInt32());
    }

    // US8-S18 (the event's fields)
    [Fact]
    public async Task EachLoad_LogsWhoReadWhatAndWhen()
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync());
        var (managerId, managerEmail) = await CreateManagerAsync(community);
        await CreatePropertyAsync(community);
        using var manager = await ClientForAsync(managerEmail);

        await ListAsync(manager, community, "?search=keystone");

        var e = Assert.Single(AccessEvents(managerId, community, "communities/residents"));
        Assert.Equal(Clock.UtcNow, (DateTimeOffset)((Serilog.Events.ScalarValue)e.Properties["UtcNow"]).Value!);
    }

    private async Task<(Guid Community, Dictionary<int, Guid> Homes, string ManagerId, HttpClient Manager)> CommunityWith120HomesAsync()
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync());
        var (managerId, managerEmail) = await CreateManagerAsync(community);
        var homes = await WithDbAsync(async db =>
        {
            var ids = new Dictionary<int, Guid>();
            // Inserted out of order, so the list's order comes from the query.
            foreach (var n in Enumerable.Range(1, 120).OrderByDescending(n => n % 7).ThenBy(n => n))
            {
                var id = Guid.NewGuid();
                ids[n] = id;
                db.Properties.Add(new Domain.Entities.Property
                {
                    Id = id, AccountNumber = $"KP-{id:N}", CommunityId = community, Address = $"{Street} #{n}", City = "Raleigh",
                    State = "NC", Zip = "27601", Lot = $"{n}", Section = "1", FiscalYear = 2026, YearBuilt = 2005, Status = "active",
                    MonthlyAssessment = 250m, AnnualAssessment = 3000m, AssessmentDueDay = 1, LateFeeAmount = 50m,
                    LateFeeGraceDays = 15, FinanceChargeRate = 0.015m,
                });
                // #114 to #120 have nobody living there.
                if (n > 113)
                    continue;
                var userId = $"resident-{Guid.NewGuid():N}";
                var email = $"resident-{n}@example.com";
                db.Users.Add(new ApplicationUser
                {
                    Id = userId, Email = email, NormalizedEmail = email.ToUpperInvariant(), UserName = $"{userId}@example.com",
                    NormalizedUserName = $"{userId}@EXAMPLE.COM", FirstName = "Resident", LastName = $"{n}",
                    SecurityStamp = Guid.NewGuid().ToString(),
                });
                db.UserProperties.Add(new UserProperty
                {
                    Id = Guid.NewGuid(), UserId = userId, PropertyId = id, LinkedAt = new DateTimeOffset(2025, 6, 1, 14, 0, 0, TimeSpan.Zero),
                });
            }
            await db.SaveChangesAsync();
            return ids;
        });
        await InsertInvitationAsync(community, homes[40], "scheduled@example.com", managerId, InvitationState.Scheduled);
        await InsertInvitationAsync(community, homes[41], "pending@example.com", managerId, InvitationState.Pending);
        await InsertInvitationAsync(community, homes[42], "late@example.com", managerId, InvitationState.Pending, expired: true);
        return (community, homes, managerId, await ClientForAsync(managerEmail));
    }

    private Task InsertInvitationAsync(Guid community, Guid home, string email, string senderId, InvitationState state,
        bool expired = false) => WithDbAsync(async db =>
    {
        var companyId = await db.Communities.Where(c => c.Id == community).Select(c => c.ManagementCompanyId).SingleAsync();
        var sentAt = expired ? Clock.UtcNow.AddHours(-80) : Clock.UtcNow.AddHours(-1);
        db.Invitations.Add(new Invitation
        {
            Id = Guid.NewGuid(), Kind = InvitationKind.Residency, CompanyId = companyId, CommunityId = community, PropertyId = home,
            Email = email, NormalizedEmail = Invitation.Normalize(email), State = state, SenderUserId = senderId,
            CreatedAt = sentAt, SentAt = state == InvitationState.Scheduled ? null : sentAt,
            ExpiresAt = state == InvitationState.Scheduled ? null : sentAt.AddHours(72),
            TokenHash = state == InvitationState.Pending ? Features.Invitations.InvitationTokens.Hash(Guid.NewGuid().ToString()) : null,
            RevokedAt = state == InvitationState.Revoked ? Clock.UtcNow : null,
            RevokeReason = state == InvitationState.Revoked ? InvitationRevokeReason.Manual : null,
        });
        await db.SaveChangesAsync();
    });

    private static async Task<JsonElement> ListAsync(HttpClient client, Guid community, string query)
    {
        var res = await client.GetAsync($"/api/v1/communities/{community}/residents{query}");
        Assert.True(res.StatusCode == HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        return await JsonAsync(res);
    }

    private static List<Guid> Ids(JsonElement list) =>
        list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("propertyId").GetGuid()).ToList();

    private List<Serilog.Events.LogEvent> AccessEvents(string actorId, Guid community, string resource) =>
        LogSink.Events.Where(e => e.MessageTemplate.Text.StartsWith("BoardAssociationDataAccess")
            && e.Properties["ActorId"].ToString() == $"\"{actorId}\""
            && e.Properties["CommunityId"].ToString() == community.ToString()
            && e.Properties["Resource"].ToString() == $"\"{resource}\"").ToList();
}
