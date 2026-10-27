using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T019 — the resolver's FR-004a decision. The expected table below restates research R2
/// independently of the implementation: who holds each community capability, and how a community's
/// status changes the answer for an authorized caller.
/// </summary>
public class ScopeDecisionTheoryTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    public enum Caller
    {
        CompanyAdministrator,
        CompanyAdministratorOfAnotherCompany,
        ManagerOfThisCommunity,
        ManagerOfAnotherCommunity,
        BoardMember,
        Accountant,
        Resident,
        NoRole,
        AdministratorAndManager,
    }

    private static readonly CommunityCapability[] AllCapabilities = Enum.GetValues<CommunityCapability>();

    // R2: capabilities a Company Administrator grant confers. Never ViewAssociationData or the
    // three 027 ARC application capabilities (FR-003).
    private static readonly HashSet<CommunityCapability> AdministratorGrants =
    [
        CommunityCapability.ManageMemberships, CommunityCapability.ManageCommunityManagers,
        CommunityCapability.ReplaceLastManager, CommunityCapability.EditCommunityProfile,
        CommunityCapability.ManageResidents, CommunityCapability.ViewArcSettings,
        CommunityCapability.ManageArcSettings, CommunityCapability.ManageCommunityLifecycle,
        CommunityCapability.ViewCommunitySettings,
    ];

    private static readonly HashSet<CommunityCapability> ManagerGrants =
    [
        CommunityCapability.ViewAssociationData, CommunityCapability.ManageMemberships,
        CommunityCapability.ViewArchitecturalApplications, CommunityCapability.ManageArchitecturalReview,
        CommunityCapability.ManageCommunityManagers, CommunityCapability.EditCommunityProfile,
        CommunityCapability.ManageResidents, CommunityCapability.ViewArcSettings,
        CommunityCapability.ManageArcSettings, CommunityCapability.ViewCommunitySettings,
    ];

    private static readonly HashSet<CommunityCapability> BoardMemberGrants =
    [
        CommunityCapability.ViewAssociationData, CommunityCapability.ViewArchitecturalApplications,
        CommunityCapability.VoteArchitecturalApplications, CommunityCapability.ViewArcSettings,
    ];

    private static readonly HashSet<CommunityCapability> AccountantGrants = [CommunityCapability.ViewAssociationData];

    public static IEnumerable<object[]> Cases() =>
        from caller in Enum.GetValues<Caller>()
        from status in new[] { CommunityStatus.Onboarding, CommunityStatus.Active, CommunityStatus.Archived }
        select new object[] { caller, status };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Decision_FollowsTheR2Table_AndTheFR004aStatusRule(Caller caller, CommunityStatus status)
    {
        var companyId = await CreateCompanyAsync();
        var communityId = await CreateCommunityInAsync(companyId, status);
        var userId = await ArrangeCallerAsync(caller, companyId, communityId);

        using var scope = NewScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ICommunityScopeResolver>();
        var principal = Principal(userId);

        foreach (var capability in AllCapabilities)
        {
            foreach (var op in new[] { ScopeOperation.Read, ScopeOperation.Write })
            {
                var expected = Expected(caller, capability, status, op);
                var actual = await resolver.DecideAsync(principal, communityId, capability, op);
                Assert.True(expected == actual,
                    $"{caller} · {capability} · {status} · {op}: expected {expected}, got {actual}");
            }

            // CanAccessAsync is the yes/no wrapper: the read decision is Allowed.
            Assert.Equal(
                Expected(caller, capability, status, ScopeOperation.Read) == ScopeDecision.Allowed,
                await resolver.CanAccessAsync(principal, communityId, capability));
        }
    }

    [Fact]
    public async Task NonexistentCommunity_IsForbiddenForEveryone_IncludingAdministrators()
    {
        var companyId = await CreateCompanyAsync();
        var (adminId, _) = await CreateCompanyAdminAsync(companyId);
        using var scope = NewScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ICommunityScopeResolver>();

        foreach (var capability in AllCapabilities)
            Assert.Equal(ScopeDecision.Forbidden,
                await resolver.DecideAsync(Principal(adminId), Guid.NewGuid(), capability, ScopeOperation.Read));
    }

    [Theory]
    [InlineData(MembershipStatus.Inactive, 0)]   // deactivated
    [InlineData(MembershipStatus.Active, -1)]    // end date passed yesterday
    public async Task EndedOrPastEndDateMembership_ConfersNothing(MembershipStatus membershipStatus, int endDaysFromToday)
    {
        var companyId = await CreateCompanyAsync();
        var communityId = await CreateCommunityInAsync(companyId);
        var endDate = endDaysFromToday == 0 ? (DateOnly?)null : Today.AddDays(endDaysFromToday);
        var (userId, _) = await CreateStaffAsync(communityId, CommunityRole.CommunityManager, endDate, membershipStatus);

        using var scope = NewScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ICommunityScopeResolver>();
        foreach (var capability in AllCapabilities)
            Assert.Equal(ScopeDecision.Forbidden,
                await resolver.DecideAsync(Principal(userId), communityId, capability, ScopeOperation.Read));
    }

    // US1-S7 (the access half): a membership in the master never reaches a sub-association.
    [Fact]
    public async Task MembershipInParent_NeverReachesTheSubAssociation()
    {
        var companyId = await CreateCompanyAsync();
        var masterId = await CreateCommunityInAsync(companyId, name: $"master-{Guid.NewGuid():N}");
        var subId = await CreateCommunityInAsync(companyId, parentId: masterId);
        var (boardId, _) = await CreateStaffAsync(masterId, CommunityRole.BoardMember);
        var (managerId, _) = await CreateManagerAsync(masterId);

        using var scope = NewScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ICommunityScopeResolver>();
        foreach (var capability in AllCapabilities)
        {
            Assert.Equal(ScopeDecision.Forbidden, await resolver.DecideAsync(Principal(boardId), subId, capability, ScopeOperation.Read));
            Assert.Equal(ScopeDecision.Forbidden, await resolver.DecideAsync(Principal(managerId), subId, capability, ScopeOperation.Read));
        }
    }

    [Fact]
    public async Task IsCompanyAdministrator_IsTrueOnlyForALiveGrant_InTheNamedCompany()
    {
        var companyId = await CreateCompanyAsync();
        var otherCompanyId = await CreateCompanyAsync();
        var (adminId, _) = await CreateCompanyAdminAsync(companyId);
        var (revokedId, _) = await CreateCompanyAdminAsync(companyId);
        await WithDbAsync(async db =>
        {
            var grant = db.CompanyAdministratorGrants.Single(g => g.UserId == revokedId);
            grant.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });

        using var scope = NewScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ICommunityScopeResolver>();
        Assert.True(await resolver.IsCompanyAdministratorAsync(Principal(adminId)));
        Assert.True(await resolver.IsCompanyAdministratorAsync(Principal(adminId), companyId));
        Assert.False(await resolver.IsCompanyAdministratorAsync(Principal(adminId), otherCompanyId));
        Assert.False(await resolver.IsCompanyAdministratorAsync(Principal(revokedId)));
    }

    private static ScopeDecision Expected(Caller caller, CommunityCapability capability, CommunityStatus status, ScopeOperation op)
    {
        var viaGrant = caller is Caller.CompanyAdministrator or Caller.AdministratorAndManager
                       && AdministratorGrants.Contains(capability);
        var viaMembership = caller switch
        {
            Caller.ManagerOfThisCommunity or Caller.AdministratorAndManager => ManagerGrants.Contains(capability),
            Caller.BoardMember => BoardMemberGrants.Contains(capability),
            Caller.Accountant => AccountantGrants.Contains(capability),
            _ => false,
        };

        if (!viaGrant && !viaMembership)
            return ScopeDecision.Forbidden;
        if (status != CommunityStatus.Archived)
            return ScopeDecision.Allowed;

        // Archived (FR-004a, FR-012): a Company Administrator still reads the settings sections and
        // may restore and end memberships; everything else is refused as archived.
        return viaGrant && (op == ScopeOperation.Read || capability == CommunityCapability.ManageCommunityLifecycle)
            ? ScopeDecision.Allowed
            : ScopeDecision.Archived;
    }

    private async Task<string> ArrangeCallerAsync(Caller caller, Guid companyId, Guid communityId)
    {
        switch (caller)
        {
            case Caller.CompanyAdministrator:
                return (await CreateCompanyAdminAsync(companyId)).UserId;
            case Caller.CompanyAdministratorOfAnotherCompany:
                return (await CreateCompanyAdminAsync(await CreateCompanyAsync())).UserId;
            case Caller.ManagerOfThisCommunity:
                return (await CreateManagerAsync(communityId)).UserId;
            case Caller.ManagerOfAnotherCommunity:
                return (await CreateManagerAsync(await CreateCommunityInAsync(companyId))).UserId;
            case Caller.BoardMember:
                return (await CreateStaffAsync(communityId, CommunityRole.BoardMember)).UserId;
            case Caller.Accountant:
                return (await CreateStaffAsync(communityId, CommunityRole.Accountant)).UserId;
            case Caller.Resident:
                return (await CreateResidentAsync(await CreatePropertyAsync(communityId))).UserId;
            case Caller.NoRole:
                return await WithDbAsync(db => CreateUserAsync(db));
            case Caller.AdministratorAndManager:
                var (userId, _) = await CreateManagerAsync(communityId);
                await WithDbAsync(async db =>
                {
                    db.CompanyAdministratorGrants.Add(new Domain.Entities.CompanyAdministratorGrant
                    {
                        Id = Guid.NewGuid(), CompanyId = companyId, UserId = userId, Source = CompanyGrantSource.Setup,
                    });
                    await db.SaveChangesAsync();
                });
                return userId;
            default:
                throw new ArgumentOutOfRangeException(nameof(caller));
        }
    }
}
