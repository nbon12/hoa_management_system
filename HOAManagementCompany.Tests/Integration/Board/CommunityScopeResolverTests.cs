using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board;

/// <summary>
/// T028 — the community-scope resolver (FR-012/FR-014), a 95%-coverage-critical file.
/// Covers spec User Story 2 (server-side scope from persisted membership) and the
/// 2026-08-23 clarifications: no master/sub cascade, and union across role rows.
/// Theory data varies role × membership status × target community per the constitution.
/// </summary>
public class CommunityScopeResolverTests(TestDatabaseFixture fixture) : BoardTestBase(fixture)
{
    private async Task<bool> ResolveAsync(string userId, Guid communityId, CommunityCapability cap)
    {
        using var scope = NewScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ICommunityScopeResolver>();
        return await resolver.CanAccessAsync(Principal(userId), communityId, cap);
    }

    // FR-012: role → capability grant matrix. BoardMember/Manager/Accountant may
    // ViewAssociationData; only Manager may ManageMemberships; Resident may neither.
    [Theory]
    [InlineData(CommunityRole.BoardMember, CommunityCapability.ViewAssociationData, true)]
    [InlineData(CommunityRole.CommunityManager, CommunityCapability.ViewAssociationData, true)]
    [InlineData(CommunityRole.Accountant, CommunityCapability.ViewAssociationData, true)]
    [InlineData(CommunityRole.Resident, CommunityCapability.ViewAssociationData, false)]
    [InlineData(CommunityRole.BoardMember, CommunityCapability.ManageMemberships, false)]
    [InlineData(CommunityRole.CommunityManager, CommunityCapability.ManageMemberships, true)]
    [InlineData(CommunityRole.Accountant, CommunityCapability.ManageMemberships, false)]
    [InlineData(CommunityRole.Resident, CommunityCapability.ManageMemberships, false)]
    public async Task RoleCapabilityMatrix(CommunityRole role, CommunityCapability cap, bool expected)
    {
        string userId;
        Guid communityId;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db);
            userId = await CreateUserAsync(db);
            await AddMembershipAsync(db, userId, communityId, role);
        }

        Assert.Equal(expected, await ResolveAsync(userId, communityId, cap));
    }

    // FR-010: a membership confers no permission unless active AND (no end date OR
    // end date >= today UTC). Varies status × end-date offset.
    [Theory]
    [InlineData(MembershipStatus.Active, null, true)]     // active, open-ended
    [InlineData(MembershipStatus.Active, 30, true)]       // active, future term
    [InlineData(MembershipStatus.Active, -1, false)]      // expired term (EndDate in the past)
    [InlineData(MembershipStatus.Inactive, null, false)]  // inactive membership
    [InlineData(MembershipStatus.Inactive, 30, false)]    // inactive even with future end date
    public async Task StatusAndTermMatrix(MembershipStatus status, int? endDayOffset, bool expected)
    {
        string userId;
        Guid communityId;
        DateOnly? end = endDayOffset is null ? null : Today.AddDays(endDayOffset.Value);
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db);
            userId = await CreateUserAsync(db);
            await AddMembershipAsync(db, userId, communityId, CommunityRole.BoardMember, status, endDate: end);
        }

        Assert.Equal(expected, await ResolveAsync(userId, communityId, CommunityCapability.ViewAssociationData));
    }

    // Edge case: an inactive/offboarded community confers no access to anyone,
    // regardless of an otherwise-valid active membership.
    [Fact]
    public async Task InactiveCommunity_DeniesEvenActiveManager()
    {
        string userId;
        Guid communityId;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db, CommunityStatus.Inactive);
            userId = await CreateUserAsync(db);
            await AddMembershipAsync(db, userId, communityId, CommunityRole.CommunityManager);
        }

        Assert.False(await ResolveAsync(userId, communityId, CommunityCapability.ViewAssociationData));
        Assert.False(await ResolveAsync(userId, communityId, CommunityCapability.ManageMemberships));
    }

    // Clarifications 2026-08-23 (a): NO cascade. A member of the master is denied
    // scope over a sub-association, and a member of the sub is denied over the master.
    [Fact]
    public async Task NoCascade_MasterAndSubMembershipsDoNotCrossOver()
    {
        string masterMemberId, subMemberId;
        Guid masterId, subId;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            masterId = await CreateCommunityAsync(db);
            subId = await CreateCommunityAsync(db, parentCommunityId: masterId);

            masterMemberId = await CreateUserAsync(db);
            await AddMembershipAsync(db, masterMemberId, masterId, CommunityRole.BoardMember);

            subMemberId = await CreateUserAsync(db);
            await AddMembershipAsync(db, subMemberId, subId, CommunityRole.BoardMember);
        }

        // Master member has scope over master, NOT over the sub.
        Assert.True(await ResolveAsync(masterMemberId, masterId, CommunityCapability.ViewAssociationData));
        Assert.False(await ResolveAsync(masterMemberId, subId, CommunityCapability.ViewAssociationData));

        // Sub member has scope over the sub, NOT over the master.
        Assert.True(await ResolveAsync(subMemberId, subId, CommunityCapability.ViewAssociationData));
        Assert.False(await ResolveAsync(subMemberId, masterId, CommunityCapability.ViewAssociationData));
    }

    // Clarifications 2026-08-23 (b): UNION. A user holding BOTH BoardMember and
    // Accountant rows in one community receives the union of both roles' capabilities.
    [Fact]
    public async Task Union_TwoRolesInOneCommunity_GrantsCombinedCapabilities()
    {
        string userId;
        Guid communityId;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db);
            userId = await CreateUserAsync(db);
            await AddMembershipAsync(db, userId, communityId, CommunityRole.BoardMember);
            await AddMembershipAsync(db, userId, communityId, CommunityRole.Accountant);
        }

        // ViewAssociationData is granted by either row.
        Assert.True(await ResolveAsync(userId, communityId, CommunityCapability.ViewAssociationData));
        // Neither BoardMember nor Accountant grants ManageMemberships, so the union does not either.
        Assert.False(await ResolveAsync(userId, communityId, CommunityCapability.ManageMemberships));
    }

    // FR-012 union upgrade: adding a Community Manager row lifts the union to include
    // ManageMemberships while the other role continues to grant ViewAssociationData.
    [Fact]
    public async Task Union_ManagerPlusAccountant_GrantsBothCapabilities()
    {
        string userId;
        Guid communityId;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db);
            userId = await CreateUserAsync(db);
            await AddMembershipAsync(db, userId, communityId, CommunityRole.Accountant);
            await AddMembershipAsync(db, userId, communityId, CommunityRole.CommunityManager);
        }

        Assert.True(await ResolveAsync(userId, communityId, CommunityCapability.ViewAssociationData));
        Assert.True(await ResolveAsync(userId, communityId, CommunityCapability.ManageMemberships));
    }

    // FR-014 / FR-016: an unknown user and a caller with no membership in the target
    // community are both denied (fail closed, no disclosure).
    [Fact]
    public async Task NoMembership_IsDenied()
    {
        string userId;
        Guid communityId;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db);
            userId = await CreateUserAsync(db); // no membership added
        }

        Assert.False(await ResolveAsync(userId, communityId, CommunityCapability.ViewAssociationData));
        Assert.False(await ResolveAsync("nonexistent-user-id", communityId, CommunityCapability.ViewAssociationData));
    }
}
