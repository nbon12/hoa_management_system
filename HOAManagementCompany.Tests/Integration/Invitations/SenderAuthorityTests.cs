using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Invitations;

/// <summary>
/// 030 T050 — <see cref="SenderAuthority"/> (FR-022; research R6): when a change takes a user's
/// authority away, every open invitation they can no longer send is revoked in that change's
/// transaction, and the ones they can still send stay open.
/// </summary>
public class SenderAuthorityTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [Fact]
    public async Task RevokeOrphaned_RevokesOnlyWhatTheUserCanNoLongerSend()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var property = await CreatePropertyAsync(community);
        var (userId, _) = await CreateCompanyAdminAsync(companyId);
        // The user is also this community's manager, so residency invitations stay theirs to send.
        await WithDbAsync(db => AddMembershipAsync(db, userId, community, CommunityRole.CommunityManager));
        var adminInvite = await AddOpenAsync(InvitationKind.CompanyAdministrator, companyId, null, null, userId);
        var residentInvite = await AddOpenAsync(InvitationKind.Residency, companyId, community, property, userId);
        var someoneElses = await AddOpenAsync(InvitationKind.CompanyAdministrator, companyId, null, null,
            (await CreateCompanyAdminAsync(companyId)).UserId);

        var revoked = await WithDbAsync(async db =>
        {
            await db.CompanyAdministratorGrants.Where(g => g.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.RevokedAt, DateTimeOffset.UtcNow));
            using var scope = NewScope();
            var sp = scope.ServiceProvider;
            var scopedDb = sp.GetRequiredService<ApplicationDbContext>();
            return await CommunityLocks.RunAsync(scopedDb, community, async () =>
            {
                var count = await sp.GetRequiredService<SenderAuthority>()
                    .RevokeOrphanedAsync(userId, null, "actor-1", "CompanyAdministratorRevoked", default);
                await scopedDb.SaveChangesAsync();
                return count;
            }, default);
        });

        Assert.Equal(1, revoked);
        var states = await WithDbAsync(db => db.Invitations.AsNoTracking()
            .Where(i => i.Id == adminInvite || i.Id == residentInvite || i.Id == someoneElses)
            .ToDictionaryAsync(i => i.Id, i => (i.State, i.RevokeReason)));
        Assert.Equal((InvitationState.Revoked, InvitationRevokeReason.SenderLostAuthority), states[adminInvite]);
        Assert.Equal((InvitationState.Pending, (InvitationRevokeReason?)null), states[residentInvite]);
        Assert.Equal((InvitationState.Pending, (InvitationRevokeReason?)null), states[someoneElses]);
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("InvitationChange")
            && e.Properties["InvitationId"].ToString() == adminInvite.ToString()
            && e.Properties["Reason"].ToString() == "\"SenderLostAuthority\""
            && e.Properties["ActorId"].ToString() == "\"actor-1\""
            && e.Properties["Cause"].ToString() == "\"CompanyAdministratorRevoked\"");
    }

    [Fact]
    public async Task CouldSend_PassesASetupStepInvitation_WithNoSender()
    {
        var companyId = await CreateCompanyAsync();
        var invitation = new Invitation { Kind = InvitationKind.CompanyAdministrator, CompanyId = companyId };

        using var scope = NewScope();
        var authority = scope.ServiceProvider.GetRequiredService<SenderAuthority>();

        Assert.True(await authority.CouldSendAsync(null, invitation, default));
        Assert.False(await authority.CouldSendAsync(await WithDbAsync(CreateUserAsync), invitation, default));
    }

    private Task<Guid> AddOpenAsync(InvitationKind kind, Guid companyId, Guid? communityId, Guid? propertyId, string senderId) =>
        WithDbAsync(async db =>
        {
            var id = Guid.NewGuid();
            var email = $"open-{id:N}@example.com";
            db.Invitations.Add(new Invitation
            {
                Id = id, Kind = kind, CompanyId = companyId, CommunityId = communityId, PropertyId = propertyId,
                Email = email, NormalizedEmail = email, State = InvitationState.Pending, SenderUserId = senderId,
                CreatedAt = Clock.UtcNow, TokenHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
                SentAt = Clock.UtcNow, ExpiresAt = Clock.UtcNow.AddHours(72),
            });
            await db.SaveChangesAsync();
            return id;
        });
}
