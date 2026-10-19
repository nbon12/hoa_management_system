using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T095 — SC-005 / FR-012: every write into an Archived community is refused. Its own members
/// hear 409 <c>COMMUNITY_ARCHIVED</c>; anyone else gets the same 403 as for a community that doesn't
/// exist (US3-S6). The one exception is a Company Administrator ending a manager membership.
/// </summary>
public class ArchivedCommunityWritesTheoryTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    public enum Caller { Manager, BoardMember, CompanyAdmin }

    /// <summary>One write route, with the member of the community who could make it while it was live.</summary>
    public sealed record WriteRow(HttpMethod Method, string Path, Func<Arranged, object> Body, Caller Caller);

    /// <summary>What <see cref="ArrangeAsync"/> builds: an Archived community with work in it.</summary>
    public sealed record Arranged(
        Guid CommunityId, string ManagerId, string BoardMemberId, string AccountantId, Guid ManagerMembershipId,
        Guid OpenApplicationId, Guid ReachedApplicationId, Guid ClosedApplicationId, Guid InvitationId,
        string CompanyAdminEmail, string ManagerEmail, string BoardMemberEmail, Guid HomeId, string ResidentId);

    /// <summary>
    /// The registry of community writes. Each story adds its rows: US3 (profile, parent, ARC),
    /// US2 (memberships and invitations), US5 (ARC settings) and US8 (residents).
    /// </summary>
    private static readonly Dictionary<string, WriteRow> Rows = new()
    {
        // US3
        ["profile PATCH"] = new(HttpMethod.Patch, "/communities/{communityId}/profile",
            _ => new { county = "Wake" }, Caller.Manager),
        ["parent change"] = new(HttpMethod.Put, "/communities/{communityId}/parent",
            _ => new { parentCommunityId = (Guid?)null }, Caller.CompanyAdmin),
        ["ARC vote"] = new(HttpMethod.Post, "/communities/{communityId}/architectural-applications/{openApplicationId}/votes",
            _ => new { choice = "Approve" }, Caller.BoardMember),
        ["ARC info request"] = new(HttpMethod.Post,
            "/communities/{communityId}/architectural-applications/{openApplicationId}/info-requests",
            _ => new { message = "Please send the fence height." }, Caller.BoardMember),
        ["record outcome"] = new(HttpMethod.Post,
            "/communities/{communityId}/architectural-applications/{reachedApplicationId}/outcome",
            _ => new { }, Caller.Manager),
        ["resend outcome email"] = new(HttpMethod.Post,
            "/communities/{communityId}/architectural-applications/{closedApplicationId}/outcome/resend-email",
            _ => new { }, Caller.Manager),
        // US2 (T081)
        ["membership create"] = new(HttpMethod.Post, "/communities/{communityId}/memberships",
            a => new { userId = a.AccountantId, role = (int)CommunityRole.BoardMember }, Caller.Manager),
        ["membership invite by email"] = new(HttpMethod.Post, "/communities/{communityId}/memberships",
            _ => new { email = $"new-{Guid.NewGuid():N}@example.com", role = (int)CommunityRole.BoardMember }, Caller.Manager),
        ["membership end date"] = new(HttpMethod.Patch, "/communities/{communityId}/memberships/{managerMembershipId}",
            _ => new { endDate = "2030-12-31" }, Caller.Manager),
        ["membership end by a manager"] = new(HttpMethod.Patch, "/communities/{communityId}/memberships/{managerMembershipId}",
            _ => new { end = true }, Caller.Manager),
        ["replace manager"] = new(HttpMethod.Post, "/communities/{communityId}/managers/replace",
            a => new { currentManagerUserId = a.ManagerId, replacementUserId = a.AccountantId }, Caller.CompanyAdmin),
        ["invitation resend"] = new(HttpMethod.Post, "/communities/{communityId}/invitations/{invitationId}/resend",
            _ => new { }, Caller.Manager),
        ["invitation revoke"] = new(HttpMethod.Post, "/communities/{communityId}/invitations/{invitationId}/revoke",
            _ => new { }, Caller.Manager),
        // US5 (T129)
        ["ARC settings save"] = new(HttpMethod.Put, "/communities/{communityId}/architectural-settings",
            _ => new
            {
                reviewPeriodDays = 45, lapseRule = "FlagOverdueOnly", decisionRule = "MajorityOfMembers", reminderDays = 7,
                timeZoneId = "America/New_York", formalDisapprovalStatement = "The board does not approve this application.",
            }, Caller.Manager),
        ["ARC settings reset"] = new(HttpMethod.Post, "/communities/{communityId}/architectural-settings/reset",
            _ => new { }, Caller.Manager),
        // US8 (T113)
        ["resident invite"] = new(HttpMethod.Post, "/communities/{communityId}/residents/invitations",
            a => new { propertyId = a.HomeId, email = $"kim-{Guid.NewGuid():N}@example.com" }, Caller.Manager),
        ["resident remove"] = new(HttpMethod.Delete, "/communities/{communityId}/residents/{homeId}/{residentId}",
            _ => new { }, Caller.Manager),
    };

    public static IEnumerable<object[]> ArchivedWriteEndpoints => Rows.Keys.Select(k => new object[] { k });

    // SC-005; US3-S6 (first half: a Board Member voting is told the community is archived)
    [Theory]
    [MemberData(nameof(ArchivedWriteEndpoints))]
    public async Task AWrite_ByAnAuthorizedMember_IsRefusedAsArchived_AndChangesNothing(string row)
    {
        var a = await ArrangeAsync();
        var write = Rows[row];
        using var client = await ClientForAsync(write.Caller switch
        {
            Caller.Manager => a.ManagerEmail,
            Caller.BoardMember => a.BoardMemberEmail,
            _ => a.CompanyAdminEmail,
        });
        var before = await SnapshotAsync(a.CommunityId);

        var res = await SendAsync(client, write, a, a.CommunityId);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("COMMUNITY_ARCHIVED", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Equal(before, await SnapshotAsync(a.CommunityId));
        Assert.Empty(Email.Sent);
    }

    // US3-S6 (second half)
    [Theory]
    [MemberData(nameof(ArchivedWriteEndpoints))]
    public async Task AWrite_ByAnOutsider_GetsTheSame403_AsForACommunityThatDoesntExist(string row)
    {
        var a = await ArrangeAsync();
        var write = Rows[row];
        var otherCommunity = await CreateCommunityInAsync(await CreateCompanyAsync());
        var (_, outsiderEmail) = await CreateStaffAsync(otherCommunity, CommunityRole.CommunityManager);
        using var outsider = await ClientForAsync(outsiderEmail);
        var before = await SnapshotAsync(a.CommunityId);

        var archived = await SendAsync(outsider, write, a, a.CommunityId);
        var missing = await SendAsync(outsider, write, a, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Forbidden, archived.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(archived)).GetProperty("code").GetString());
        Assert.Equal(missing.StatusCode, archived.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await archived.Content.ReadAsStringAsync());
        Assert.Equal(before, await SnapshotAsync(a.CommunityId));
    }

    // FR-012 exception (US3-S15): a Company Administrator may still end a manager membership.
    [Fact]
    public async Task ACompanyAdministrator_EndingTheLastManager_WhileArchived_Succeeds()
    {
        var a = await ArrangeAsync();
        using var admin = await ClientForAsync(a.CompanyAdminEmail);

        var res = await admin.PatchAsJsonAsync(
            $"/api/v1/communities/{a.CommunityId}/memberships/{a.ManagerMembershipId}", new { end = true });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var row = await WithDbAsync(db => db.CommunityMemberships.AsNoTracking().SingleAsync(m => m.Id == a.ManagerMembershipId));
        Assert.Equal(MembershipStatus.Inactive, row.Status);
        Assert.Equal(0, await WithDbAsync(db => db.CommunityMemberships.CountAsync(m => m.CommunityId == a.CommunityId
            && m.Role == CommunityRole.CommunityManager && m.Status == MembershipStatus.Active)));
    }

    private async Task<Arranged> ArrangeAsync()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (managerId, managerEmail) = await CreateManagerAsync(community);
        var (boardId, boardEmail) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        var (accountantId, _) = await CreateStaffAsync(community, CommunityRole.Accountant);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        var home = await CreatePropertyAsync(community);
        var (residentId, _) = await CreateResidentAsync(home);
        var open = await CreateArcApplicationAsync(community, home, 1);
        var reached = await CreateArcApplicationAsync(community, home, 2, ArcApplicationStatus.DecisionReached);
        var closed = await CreateArcApplicationAsync(community, home, 3, ArcApplicationStatus.Closed);
        var managerMembership = await WithDbAsync(db => db.CommunityMemberships
            .Where(m => m.CommunityId == community && m.UserId == managerId).Select(m => m.Id).SingleAsync());
        var invitationId = await WithDbAsync(async db =>
        {
            var invitation = new Invitation
            {
                Id = Guid.NewGuid(), Kind = InvitationKind.Membership, CompanyId = companyId, CommunityId = community,
                Role = CommunityRole.BoardMember, Email = "pending@example.com",
                NormalizedEmail = Invitation.Normalize("pending@example.com"), State = InvitationState.Pending,
                SenderUserId = managerId, CreatedAt = Clock.UtcNow,
                TokenHash = Features.Invitations.InvitationTokens.Hash(Features.Invitations.InvitationTokens.New()),
                SentAt = Clock.UtcNow.AddMinutes(-10), ExpiresAt = Clock.UtcNow.AddHours(72),
            };
            db.Invitations.Add(invitation);
            await db.SaveChangesAsync();
            return invitation.Id;
        });
        // Archived after the work exists, the way the archive action leaves it.
        await WithDbAsync(db => db.Communities.Where(c => c.Id == community).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.Status, CommunityStatus.Archived).SetProperty(c => c.ArchivedAt, Clock.UtcNow)));
        return new Arranged(community, managerId, boardId, accountantId, managerMembership, open, reached, closed,
            invitationId, adminEmail, managerEmail, boardEmail, home, residentId);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, WriteRow write, Arranged a, Guid communityId)
    {
        var path = write.Path
            .Replace("{communityId}", communityId.ToString())
            .Replace("{openApplicationId}", a.OpenApplicationId.ToString())
            .Replace("{reachedApplicationId}", a.ReachedApplicationId.ToString())
            .Replace("{closedApplicationId}", a.ClosedApplicationId.ToString())
            .Replace("{managerMembershipId}", a.ManagerMembershipId.ToString())
            .Replace("{invitationId}", a.InvitationId.ToString())
            .Replace("{homeId}", a.HomeId.ToString())
            .Replace("{residentId}", a.ResidentId);
        return client.SendAsync(new HttpRequestMessage(write.Method, "/api/v1" + path)
        {
            Content = JsonContent.Create(write.Body(a)),
        });
    }

    /// <summary>Everything a refused write might have touched, as one comparable string.</summary>
    private Task<string> SnapshotAsync(Guid communityId) => WithDbAsync(async db =>
    {
        var community = await db.Communities.AsNoTracking().SingleAsync(c => c.Id == communityId);
        var memberships = await db.CommunityMemberships.AsNoTracking().Where(m => m.CommunityId == communityId)
            .OrderBy(m => m.Id).Select(m => $"{m.Id}:{m.Role}:{m.Status}:{m.EndDate}").ToListAsync();
        var apps = await db.ArchitecturalApplications.AsNoTracking().Where(x => x.CommunityId == communityId)
            .OrderBy(x => x.Id).Select(x => $"{x.Id}:{x.Status}:{x.ClosedAt}").ToListAsync();
        var votes = await db.ArchitecturalVotes.AsNoTracking().CountAsync(v => v.Application.CommunityId == communityId);
        var infoRequests = await db.ArchitecturalInfoRequests.AsNoTracking().CountAsync(r => r.Application.CommunityId == communityId);
        var invitations = await db.Invitations.AsNoTracking().Where(i => i.CommunityId == communityId)
            .OrderBy(i => i.Id).Select(i => $"{i.Id}:{i.State}:{i.SentAt}").ToListAsync();
        var residents = await db.UserProperties.AsNoTracking().CountAsync(up => up.Property.CommunityId == communityId);
        var arc = await db.CommunityArcSettings.AsNoTracking().Where(a => a.CommunityId == communityId)
            .Select(a => $"{a.ReviewPeriodDays}:{a.LapseRule}:{a.UpdatedAt}").SingleAsync();
        var outbox = await db.OutboxMessages.AsNoTracking().CountAsync();
        return string.Join("|", community.CommunityName, community.County, community.ParentCommunityId, community.Status,
            string.Join(",", memberships), string.Join(",", apps), votes, infoRequests, string.Join(",", invitations), residents, arc, outbox);
    });
}
