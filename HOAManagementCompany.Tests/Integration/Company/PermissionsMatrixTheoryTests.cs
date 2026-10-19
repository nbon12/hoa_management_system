using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T151 — SC-002 / FR-005: every action row of the spec's permissions table, for each of the
/// seven roles from Quality gates, against the real endpoint. Allowed means a 2xx; denied means
/// 403 <c>FORBIDDEN</c>. FR-021's rows (Board Member and Accountant memberships, directly and by
/// invitation) are included. Archived-community rows are in <see cref="ArchivedCommunityWritesTheoryTests"/>.
/// </summary>
public class PermissionsMatrixTheoryTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    public enum Role { CompanyAdministrator, ManagerHere, ManagerElsewhere, BoardMember, Accountant, Resident, NoRole }

    /// <summary>The arranged world: an Active community A with staff, work and residents, and another community B.</summary>
    public sealed record World(
        Guid CompanyId, Guid Community, Guid Other, Guid Home, string ResidentId, string TargetId, string CoManagerId,
        Guid CoManagerMembership, Guid AccountantMembership, string ManagerHereId, Guid PendingInvitation);

    private sealed record Row(Func<World, HttpClient, Task<HttpResponseMessage>> Call, params Role[] Allowed);

    private static readonly Dictionary<string, Row> Rows = new()
    {
        ["add a community"] = new((w, c) => c.PostAsJsonAsync("/api/v1/communities", new
        {
            legalName = "Keystone Glen HOA, Inc.", communityName = $"Keystone Glen {Guid.NewGuid():N}"[..22], county = "Wake",
            formationDate = "2004-03-14", managementStartDate = "2026-11-01",
        }), Role.CompanyAdministrator, Role.ManagerHere, Role.ManagerElsewhere),
        ["edit profile"] = new((w, c) => c.PatchAsJsonAsync($"/api/v1/communities/{w.Community}/profile", new { county = "Wake" }),
            Role.CompanyAdministrator, Role.ManagerHere),
        ["change parent"] = new((w, c) => c.PutAsJsonAsync($"/api/v1/communities/{w.Community}/parent", new { parentCommunityId = w.Other }),
            Role.CompanyAdministrator),
        ["archive"] = new((w, c) => c.PostAsync($"/api/v1/communities/{w.Community}/archive", null), Role.CompanyAdministrator),
        ["appoint a Community Manager"] = new((w, c) => c.PostAsJsonAsync($"/api/v1/communities/{w.Community}/memberships",
            new { role = (int)CommunityRole.CommunityManager, userId = w.TargetId }), Role.CompanyAdministrator, Role.ManagerHere),
        ["end another Community Manager"] = new((w, c) => c.PatchAsJsonAsync(
            $"/api/v1/communities/{w.Community}/memberships/{w.CoManagerMembership}", new { end = true }),
            Role.CompanyAdministrator, Role.ManagerHere),
        ["downgrade another Community Manager"] = new((w, c) => c.PatchAsJsonAsync(
            $"/api/v1/communities/{w.Community}/memberships/{w.CoManagerMembership}", new { downgradeTo = (int)CommunityRole.BoardMember }),
            Role.CompanyAdministrator, Role.ManagerHere),
        ["replace the last manager"] = new((w, c) => c.PostAsJsonAsync($"/api/v1/communities/{w.Community}/managers/replace",
            new { currentManagerUserId = w.CoManagerId, replacementUserId = w.TargetId }), Role.CompanyAdministrator),
        // FR-021: Board Member and Accountant memberships, directly and by invitation.
        ["grant a Board Member membership"] = new((w, c) => c.PostAsJsonAsync($"/api/v1/communities/{w.Community}/memberships",
            new { role = (int)CommunityRole.BoardMember, userId = w.TargetId }), Role.CompanyAdministrator, Role.ManagerHere),
        ["invite an Accountant"] = new((w, c) => c.PostAsJsonAsync($"/api/v1/communities/{w.Community}/memberships",
            new { role = (int)CommunityRole.Accountant, email = $"acc-{Guid.NewGuid():N}@example.com" }),
            Role.CompanyAdministrator, Role.ManagerHere),
        ["end an Accountant membership"] = new((w, c) => c.PatchAsJsonAsync(
            $"/api/v1/communities/{w.Community}/memberships/{w.AccountantMembership}", new { end = true }),
            Role.CompanyAdministrator, Role.ManagerHere),
        ["invite a resident"] = new((w, c) => c.PostAsJsonAsync($"/api/v1/communities/{w.Community}/residents/invitations",
            new { propertyId = w.Home, email = $"kim-{Guid.NewGuid():N}@example.com" }), Role.CompanyAdministrator, Role.ManagerHere),
        ["remove a resident"] = new((w, c) => c.DeleteAsync($"/api/v1/communities/{w.Community}/residents/{w.Home}/{w.ResidentId}"),
            Role.CompanyAdministrator, Role.ManagerHere),
        ["view the Residents area"] = new((w, c) => c.GetAsync($"/api/v1/communities/{w.Community}/residents"),
            Role.CompanyAdministrator, Role.ManagerHere),
        ["revoke an invitation"] = new((w, c) => c.PostAsync($"/api/v1/communities/{w.Community}/invitations/{w.PendingInvitation}/revoke", null),
            Role.CompanyAdministrator, Role.ManagerHere),
        ["edit ARC settings"] = new((w, c) => c.PutAsJsonAsync($"/api/v1/communities/{w.Community}/architectural-settings", new
        {
            reviewPeriodDays = 45, lapseRule = "FlagOverdueOnly", decisionRule = "MajorityOfMembers", reminderDays = 7,
            timeZoneId = "America/New_York", formalDisapprovalStatement = "The board does not approve this application.",
        }), Role.CompanyAdministrator, Role.ManagerHere),
        ["reset ARC settings"] = new((w, c) => c.PostAsync($"/api/v1/communities/{w.Community}/architectural-settings/reset", null),
            Role.CompanyAdministrator, Role.ManagerHere),
        ["read company defaults"] = new((w, c) => c.GetAsync("/api/v1/company/defaults"),
            Role.CompanyAdministrator, Role.ManagerHere, Role.ManagerElsewhere),
        ["change company defaults"] = new((w, c) => c.PutAsJsonAsync("/api/v1/company/defaults", new
        {
            reviewPeriodDays = 45, lapseRule = "DeemedApproved", decisionRule = "MajorityOfMembers", reminderDays = 7,
            timeZoneId = "America/New_York", formalDisapprovalStatement = "The board does not approve this application.",
        }), Role.CompanyAdministrator),
        ["portfolio view"] = new((w, c) => c.GetAsync("/api/v1/company/communities"), Role.CompanyAdministrator),
        ["grant Company Administrator"] = new((w, c) => c.PostAsJsonAsync("/api/v1/company/administrators/invitations",
            new { email = $"ops-{Guid.NewGuid():N}@example.com" }), Role.CompanyAdministrator),
    };

    public static IEnumerable<object[]> Cases() =>
        from action in Rows.Keys from role in Enum.GetValues<Role>() select new object[] { action, role };

    // SC-002, FR-005, FR-021
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EachAction_AnswersEachRole_AsThePermissionsTableSays(string action, Role role)
    {
        var row = Rows[action];
        var (world, callerId) = await ArrangeAsync(role);
        using var client = ClientWithToken(role == Role.Resident ? MintToken(callerId, world.Home, world.Community) : MintToken(callerId));

        var res = await row.Call(world, client);

        var body = await res.Content.ReadAsStringAsync();
        if (row.Allowed.Contains(role))
            Assert.True(res.IsSuccessStatusCode, $"{action} as {role}: expected allowed, got {(int)res.StatusCode} {body}");
        else
        {
            Assert.True(res.StatusCode == HttpStatusCode.Forbidden, $"{action} as {role}: expected 403, got {(int)res.StatusCode} {body}");
            Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        }
    }

    private async Task<(World World, string CallerId)> ArrangeAsync(Role role)
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var other = await CreateCommunityInAsync(companyId);
        var (managerHere, _) = await CreateManagerAsync(community);
        var (coManager, _) = await CreateManagerAsync(community);
        var (managerElsewhere, _) = await CreateManagerAsync(other);
        var (board, _) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        var (accountant, _) = await CreateStaffAsync(community, CommunityRole.Accountant);
        var (target, _) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        var (admin, _) = await CreateCompanyAdminAsync(companyId);
        var home = await CreatePropertyAsync(community);
        var (resident, _) = await CreateResidentAsync(home);
        var nobody = await WithDbAsync(CreateUserAsync);
        // Board Member "target" is in the people list; strip the role so the grant rows have someone to grant to.
        await WithDbAsync(db => db.CommunityMemberships.Where(m => m.UserId == target)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, MembershipStatus.Inactive)));
        await WithDbAsync(async db =>
        {
            db.UserProperties.Add(new UserProperty { Id = Guid.NewGuid(), UserId = target, PropertyId = await CreatePropertyInAsync(db, community) });
            await db.SaveChangesAsync();
        });
        var memberships = await WithDbAsync(db => db.CommunityMemberships.Where(m => m.CommunityId == community)
            .ToDictionaryAsync(m => (m.UserId, m.Role), m => m.Id));
        var pending = await WithDbAsync(async db =>
        {
            var invitation = new Invitation
            {
                Id = Guid.NewGuid(), Kind = InvitationKind.Membership, CompanyId = companyId, CommunityId = community,
                Role = CommunityRole.BoardMember, Email = "pending@example.com", NormalizedEmail = "pending@example.com",
                State = InvitationState.Pending, SenderUserId = managerHere, CreatedAt = Clock.UtcNow, SentAt = Clock.UtcNow.AddMinutes(-5),
                ExpiresAt = Clock.UtcNow.AddHours(72), TokenHash = Guid.NewGuid().ToString("N"),
            };
            db.Invitations.Add(invitation);
            await db.SaveChangesAsync();
            return invitation.Id;
        });

        var world = new World(companyId, community, other, home, resident, target, coManager,
            memberships[(coManager, CommunityRole.CommunityManager)], memberships[(accountant, CommunityRole.Accountant)],
            managerHere, pending);
        var caller = role switch
        {
            Role.CompanyAdministrator => admin,
            Role.ManagerHere => managerHere,
            Role.ManagerElsewhere => managerElsewhere,
            Role.BoardMember => board,
            Role.Accountant => accountant,
            Role.Resident => resident,
            _ => nobody,
        };
        return (world, caller);
    }

    private static async Task<Guid> CreatePropertyInAsync(Infrastructure.Persistence.ApplicationDbContext db, Guid community)
    {
        var id = Guid.NewGuid();
        db.Properties.Add(new Domain.Entities.Property
        {
            Id = id, AccountNumber = $"PM-{id:N}", CommunityId = community, Address = $"{Random.Shared.Next(1, 999)} Keystone Park Dr",
            City = "Raleigh", State = "NC", Zip = "27601", Lot = "1", Section = "1", FiscalYear = 2026, YearBuilt = 2005,
            Status = "active", MonthlyAssessment = 250m, AnnualAssessment = 3000m, AssessmentDueDay = 1, LateFeeAmount = 50m,
            LateFeeGraceDays = 15, FinanceChargeRate = 0.015m,
        });
        db.Owners.Add(new Owner { Id = Guid.NewGuid(), PropertyId = id, FirstName = "Pat", LastName = "Owner", Email = $"owner-{id:N}@nekohoa.dev" });
        await db.SaveChangesAsync();
        return id;
    }
}
