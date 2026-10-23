using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T020 — company-level capabilities (FR-004 checks b and c; research R2). Adding a community
/// and reading the company defaults are shared with effective Community Managers of Onboarding or
/// Active communities; the rest are Company Administrator only.
/// </summary>
public class CompanyCapabilityTheoryTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    public enum Caller
    {
        CompanyAdministrator,
        RevokedCompanyAdministrator,
        ManagerInOnboardingCommunity,
        ManagerInActiveCommunity,
        ManagerOnlyInArchivedCommunity,
        ManagerWithEndedMembership,
        ManagerPastEndDate,
        BoardMember,
        Accountant,
        Resident,
        NoRole,
    }

    private static readonly CompanyCapability[] Shared = [CompanyCapability.CreateCommunity, CompanyCapability.ViewCompanyDefaults];
    private static readonly CompanyCapability[] AdministratorOnly =
        [CompanyCapability.ManageCompanyDefaults, CompanyCapability.ManageCompanyAdministrators, CompanyCapability.ViewPortfolio];

    // US1-S5: Board Member, Accountant, Resident and no role are refused. US1-S6: a manager whose
    // only membership is in an Archived community is refused.
    [Theory]
    [InlineData(Caller.CompanyAdministrator, true, true)]
    [InlineData(Caller.RevokedCompanyAdministrator, false, false)]
    [InlineData(Caller.ManagerInOnboardingCommunity, true, false)]
    [InlineData(Caller.ManagerInActiveCommunity, true, false)]
    [InlineData(Caller.ManagerOnlyInArchivedCommunity, false, false)]
    [InlineData(Caller.ManagerWithEndedMembership, false, false)]
    [InlineData(Caller.ManagerPastEndDate, false, false)]
    [InlineData(Caller.BoardMember, false, false)]
    [InlineData(Caller.Accountant, false, false)]
    [InlineData(Caller.Resident, false, false)]
    [InlineData(Caller.NoRole, false, false)]
    public async Task CompanyCapabilities_FollowTheGrantAndEffectiveManagerRule(
        Caller caller, bool sharedAllowed, bool administratorOnlyAllowed)
    {
        var companyId = await CreateCompanyAsync();
        var userId = await ArrangeAsync(caller, companyId);

        using var scope = NewScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ICommunityScopeResolver>();
        foreach (var capability in Shared)
            Assert.True(sharedAllowed == await resolver.CanAccessCompanyAsync(Principal(userId), capability),
                $"{caller} · {capability}: expected {sharedAllowed}");
        foreach (var capability in AdministratorOnly)
            Assert.True(administratorOnlyAllowed == await resolver.CanAccessCompanyAsync(Principal(userId), capability),
                $"{caller} · {capability}: expected {administratorOnlyAllowed}");
    }

    private async Task<string> ArrangeAsync(Caller caller, Guid companyId)
    {
        switch (caller)
        {
            case Caller.CompanyAdministrator:
                return (await CreateCompanyAdminAsync(companyId)).UserId;
            case Caller.RevokedCompanyAdministrator:
            {
                var (userId, _) = await CreateCompanyAdminAsync(companyId);
                await WithDbAsync(async db =>
                {
                    db.CompanyAdministratorGrants.Single(g => g.UserId == userId).RevokedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync();
                });
                return userId;
            }
            case Caller.ManagerInOnboardingCommunity:
                return (await CreateManagerAsync(await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding))).UserId;
            case Caller.ManagerInActiveCommunity:
                return (await CreateManagerAsync(await CreateCommunityInAsync(companyId))).UserId;
            case Caller.ManagerOnlyInArchivedCommunity:
                return (await CreateManagerAsync(await CreateCommunityInAsync(companyId, CommunityStatus.Archived))).UserId;
            case Caller.ManagerWithEndedMembership:
                return (await CreateStaffAsync(await CreateCommunityInAsync(companyId), CommunityRole.CommunityManager,
                    status: MembershipStatus.Inactive)).UserId;
            case Caller.ManagerPastEndDate:
                return (await CreateManagerAsync(await CreateCommunityInAsync(companyId), Today.AddDays(-1))).UserId;
            case Caller.BoardMember:
                return (await CreateStaffAsync(await CreateCommunityInAsync(companyId), CommunityRole.BoardMember)).UserId;
            case Caller.Accountant:
                return (await CreateStaffAsync(await CreateCommunityInAsync(companyId), CommunityRole.Accountant)).UserId;
            case Caller.Resident:
                return (await CreateResidentAsync(await CreatePropertyAsync(await CreateCommunityInAsync(companyId)))).UserId;
            case Caller.NoRole:
                return await WithDbAsync(db => CreateUserAsync(db));
            default:
                throw new ArgumentOutOfRangeException(nameof(caller));
        }
    }
}
