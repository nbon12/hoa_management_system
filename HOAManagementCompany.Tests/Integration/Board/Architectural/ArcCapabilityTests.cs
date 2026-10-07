using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>
/// T012 — the three 027 capabilities through the shared resolver: who is granted each, and that
/// inactive or ended memberships confer nothing. Accountants and residents get no ARC access.
/// </summary>
public class ArcCapabilityTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    public enum MembershipState { Active, Inactive, Ended }

    public static TheoryData<CommunityRole, CommunityCapability, MembershipState, bool> Cases()
    {
        var data = new TheoryData<CommunityRole, CommunityCapability, MembershipState, bool>();
        var grants = new Dictionary<(CommunityRole, CommunityCapability), bool>
        {
            [(CommunityRole.BoardMember, CommunityCapability.ViewArchitecturalApplications)] = true,
            [(CommunityRole.BoardMember, CommunityCapability.VoteArchitecturalApplications)] = true,
            [(CommunityRole.BoardMember, CommunityCapability.ManageArchitecturalReview)] = false,
            [(CommunityRole.CommunityManager, CommunityCapability.ViewArchitecturalApplications)] = true,
            [(CommunityRole.CommunityManager, CommunityCapability.VoteArchitecturalApplications)] = false,
            [(CommunityRole.CommunityManager, CommunityCapability.ManageArchitecturalReview)] = true,
            [(CommunityRole.Accountant, CommunityCapability.ViewArchitecturalApplications)] = false,
            [(CommunityRole.Accountant, CommunityCapability.VoteArchitecturalApplications)] = false,
            [(CommunityRole.Accountant, CommunityCapability.ManageArchitecturalReview)] = false,
            [(CommunityRole.Resident, CommunityCapability.ViewArchitecturalApplications)] = false,
            [(CommunityRole.Resident, CommunityCapability.VoteArchitecturalApplications)] = false,
            [(CommunityRole.Resident, CommunityCapability.ManageArchitecturalReview)] = false,
        };
        foreach (var ((role, cap), granted) in grants)
            foreach (var state in Enum.GetValues<MembershipState>())
                data.Add(role, cap, state, granted && state == MembershipState.Active);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Resolver_GrantsArcCapabilities_ByRoleAndMembershipState(
        CommunityRole role, CommunityCapability capability, MembershipState state, bool expected)
    {
        using var scope = NewScope();
        var db = Db(scope);
        var communityId = await CreateCommunityAsync(db);
        var userId = await CreateUserAsync(db);
        await AddMembershipAsync(db, userId, communityId, role,
            status: state == MembershipState.Inactive ? MembershipStatus.Inactive : MembershipStatus.Active,
            endDate: state == MembershipState.Ended ? Today.AddDays(-1) : null);

        var resolver = scope.ServiceProvider.GetRequiredService<ICommunityScopeResolver>();
        Assert.Equal(expected, await resolver.CanAccessAsync(Principal(userId), communityId, capability));
    }
}
