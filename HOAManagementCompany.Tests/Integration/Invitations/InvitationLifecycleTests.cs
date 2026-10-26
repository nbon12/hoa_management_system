using System.Text.RegularExpressions;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Features.Invitations;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Invitations;

/// <summary>
/// 030 T047 — the invitation lifecycle shared by every kind (FR-022, FR-041c; research R6, R12),
/// exercised with Company Administrator invitations so the core is tested on its own. US2, US7 and
/// US8 test the kind-specific offers.
/// </summary>
public partial class InvitationLifecycleTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [GeneratedRegex("/invite#t=([A-Za-z0-9_-]{43})")]
    private static partial Regex LinkToken();

    [Fact]
    public async Task Create_StoresOnlyTheHash_SetsTheTimesFromTheClock_AndSendsOneEmail()
    {
        var (companyId, senderId) = await CompanyWithAdminAsync();
        var invitee = await WithDbAsync(CreateUserAsync);
        var sentAt = Clock.UtcNow = new DateTimeOffset(2026, 10, 12, 12, 30, 0, TimeSpan.Zero);

        var invitation = await CreateAsync(companyId, senderId, EmailOf(invitee));

        var token = SingleTokenSentTo(EmailOf(invitee));
        var row = await ReloadAsync(invitation.Id);
        Assert.Equal(InvitationState.Pending, row.State);
        Assert.Equal(InvitationTokens.Hash(token), row.TokenHash);
        Assert.Equal(sentAt, row.SentAt);
        Assert.Equal(sentAt.AddHours(72), row.ExpiresAt);
        Assert.Null(row.LastSendError);
        Assert.Equal(senderId, row.SenderUserId);
        var delivery = Assert.Single(await DeliveriesAsync(invitation.Id));
        Assert.True(delivery.Succeeded);
        Assert.Equal(sentAt, delivery.AttemptedAt);
        Assert.False(delivery.BypassesRecipientCap);

        // The token itself is stored nowhere.
        var stored = await WithDbAsync(db => db.Database
            .SqlQuery<int>($"""SELECT COUNT(*)::int AS "Value" FROM "Invitations" WHERE "TokenHash" = {token} OR "Email" = {token}""")
            .SingleAsync());
        Assert.Equal(0, stored);
    }

    [Fact]
    public async Task AFailedSend_LeavesItPendingWithTheError_AndALaterSendClearsIt()
    {
        var (companyId, senderId) = await CompanyWithAdminAsync();
        var invitee = await WithDbAsync(CreateUserAsync);

        Email.RejectSends = true;
        var invitation = await CreateAsync(companyId, senderId, EmailOf(invitee));
        Email.RejectSends = false;

        var failed = await ReloadAsync(invitation.Id);
        Assert.Equal(InvitationState.Pending, failed.State);
        Assert.NotNull(failed.LastSendError);
        Assert.False(Assert.Single(await DeliveriesAsync(invitation.Id)).Succeeded);

        Clock.UtcNow = Clock.UtcNow.AddSeconds(61);
        await WithServiceAsync(s => s.ResendAsync(senderId, invitation.Id, default));

        Assert.Null((await ReloadAsync(invitation.Id)).LastSendError);
        Assert.Single(TokensSentTo(EmailOf(invitee)));
    }

    [Fact]
    public async Task Resend_GivesANewLinkAndNew72Hours_AndTheResenderBecomesTheSender()
    {
        var (companyId, senderId) = await CompanyWithAdminAsync();
        var (otherAdminId, _) = await CreateCompanyAdminAsync(companyId);
        var invitee = await WithDbAsync(CreateUserAsync);
        var invitation = await CreateAsync(companyId, senderId, EmailOf(invitee));
        var oldToken = SingleTokenSentTo(EmailOf(invitee));

        var resentAt = Clock.UtcNow = Clock.UtcNow.AddHours(80);
        await WithServiceAsync(s => s.ResendAsync(otherAdminId, invitation.Id, default));

        var row = await ReloadAsync(invitation.Id);
        var newToken = TokensSentTo(EmailOf(invitee)).Last();
        Assert.NotEqual(oldToken, newToken);
        Assert.Equal(InvitationTokens.Hash(newToken), row.TokenHash);
        Assert.Equal(resentAt, row.SentAt);
        Assert.Equal(resentAt.AddHours(72), row.ExpiresAt);
        Assert.Equal(otherAdminId, row.SenderUserId);
        Assert.Equal(1, row.ResendCount);
        Assert.Contains("The link in the earlier email no longer works.", Email.Sent.Last().Body);

        var refused = await RefusalAsync(s => s.AcceptAsync(oldToken, invitee, default));
        Assert.Equal(("INVITATION_INVALID", 410), (refused.Code, refused.StatusCode));
    }

    [Fact]
    public async Task Accept_GrantsTheRole_AndMarksTheInvitationAccepted()
    {
        var (companyId, senderId) = await CompanyWithAdminAsync();
        var invitee = await WithDbAsync(CreateUserAsync);
        var invitation = await CreateAsync(companyId, senderId, EmailOf(invitee));
        var token = SingleTokenSentTo(EmailOf(invitee));

        var outcome = await WithServiceAsync(s => s.AcceptAsync(token, invitee, default));

        Assert.False(outcome.AlreadyHeld);
        var row = await ReloadAsync(invitation.Id);
        Assert.Equal(InvitationState.Accepted, row.State);
        Assert.Equal(invitee, row.AcceptedByUserId);
        Assert.Equal(Clock.UtcNow, row.AcceptedAt);
        var grant = await WithDbAsync(db => db.CompanyAdministratorGrants.SingleAsync(g => g.UserId == invitee));
        Assert.Equal((companyId, CompanyGrantSource.Invitation, invitation.Id, senderId),
            (grant.CompanyId, grant.Source, grant.InvitationId, grant.GrantedByUserId));
    }

    // FR-022 step 1: unknown, accepted or revoked links are invalid — even past their expiry.
    [Fact]
    public async Task Accept_UnknownAcceptedOrRevoked_IsInvalid_EvenWhenOld()
    {
        var (companyId, senderId) = await CompanyWithAdminAsync();
        var accepter = await WithDbAsync(CreateUserAsync);
        var revokedInvitee = await WithDbAsync(CreateUserAsync);
        await CreateAsync(companyId, senderId, EmailOf(accepter));
        var acceptedToken = SingleTokenSentTo(EmailOf(accepter));
        await WithServiceAsync(s => s.AcceptAsync(acceptedToken, accepter, default));
        var revoked = await CreateAsync(companyId, senderId, EmailOf(revokedInvitee));
        var revokedToken = SingleTokenSentTo(EmailOf(revokedInvitee));
        await WithServiceAsync(s => s.RevokeAsync(senderId, revoked.Id, default));
        Clock.UtcNow = Clock.UtcNow.AddHours(100);

        foreach (var (token, userId) in new[] { (InvitationTokens.New(), accepter), (acceptedToken, accepter), (revokedToken, revokedInvitee) })
        {
            var refused = await RefusalAsync(s => s.AcceptAsync(token, userId, default));
            Assert.Equal(("INVITATION_INVALID", 410), (refused.Code, refused.StatusCode));
        }
        Assert.Equal(InvitationState.Revoked, (await ReloadAsync(revoked.Id)).State);
    }

    // FR-022 step 2: expired comes before the sender and email checks.
    [Fact]
    public async Task Accept_PendingPastItsExpiry_IsExpired_BeforeSenderAndEmailChecks()
    {
        var (companyId, senderId) = await CompanyWithAdminAsync();
        var invitee = await WithDbAsync(CreateUserAsync);
        var someoneElse = await WithDbAsync(CreateUserAsync);
        var invitation = await CreateAsync(companyId, senderId, EmailOf(invitee));
        var token = SingleTokenSentTo(EmailOf(invitee));
        await RevokeGrantAsync(senderId);
        Clock.UtcNow = Clock.UtcNow.AddHours(72);

        var refused = await RefusalAsync(s => s.AcceptAsync(token, someoneElse, default));

        Assert.Equal(("INVITATION_EXPIRED", 410), (refused.Code, refused.StatusCode));
        Assert.Equal(InvitationState.Pending, (await ReloadAsync(invitation.Id)).State);
    }

    // FR-022 step 3: a sender who lost authority → the invitation is revoked, and the link is invalid.
    [Fact]
    public async Task Accept_WhenTheSenderLostAuthority_RevokesIt_AndRefusesAsInvalid()
    {
        var (companyId, senderId) = await CompanyWithAdminAsync();
        await CreateCompanyAdminAsync(companyId);
        var invitee = await WithDbAsync(CreateUserAsync);
        var invitation = await CreateAsync(companyId, senderId, EmailOf(invitee));
        var token = SingleTokenSentTo(EmailOf(invitee));
        await RevokeGrantAsync(senderId);

        var refused = await RefusalAsync(s => s.AcceptAsync(token, invitee, default));

        Assert.Equal(("INVITATION_INVALID", 410), (refused.Code, refused.StatusCode));
        var row = await ReloadAsync(invitation.Id);
        Assert.Equal((InvitationState.Revoked, InvitationRevokeReason.SenderLostAuthority), (row.State, row.RevokeReason));
        Assert.Null(row.RevokedByUserId);
        Assert.False(await WithDbAsync(db => db.CompanyAdministratorGrants.AnyAsync(g => g.UserId == invitee)));
    }

    // FR-022 step 4: a different signed-in account is refused without learning the invited email.
    [Fact]
    public async Task Accept_ByADifferentAccount_IsAnEmailMismatch_AndTheInvitationStaysPending()
    {
        var (companyId, senderId) = await CompanyWithAdminAsync();
        var invitee = await WithDbAsync(CreateUserAsync);
        var bob = await WithDbAsync(CreateUserAsync);
        var invitation = await CreateAsync(companyId, senderId, EmailOf(invitee));
        var token = SingleTokenSentTo(EmailOf(invitee));

        var refused = await RefusalAsync(s => s.AcceptAsync(token, bob, default));

        Assert.Equal(("INVITATION_EMAIL_MISMATCH", 403), (refused.Code, refused.StatusCode));
        Assert.DoesNotContain(EmailOf(invitee), refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(InvitationState.Pending, (await ReloadAsync(invitation.Id)).State);
        Assert.False(await WithDbAsync(db => db.CompanyAdministratorGrants.AnyAsync(g => g.UserId == bob)));

        // The invited account can still accept it.
        await WithServiceAsync(s => s.AcceptAsync(token, invitee, default));
        Assert.Equal(InvitationState.Accepted, (await ReloadAsync(invitation.Id)).State);
    }

    [Fact]
    public async Task Accept_WhenAlreadyHoldingTheRole_MarksItAccepted_AndDuplicatesNothing()
    {
        var (companyId, senderId) = await CompanyWithAdminAsync();
        var invitee = await WithDbAsync(CreateUserAsync);
        var invitation = await CreateAsync(companyId, senderId, EmailOf(invitee));
        var token = SingleTokenSentTo(EmailOf(invitee));
        await WithDbAsync(async db =>
        {
            db.CompanyAdministratorGrants.Add(new CompanyAdministratorGrant
            {
                Id = Guid.NewGuid(), CompanyId = companyId, UserId = invitee, Source = CompanyGrantSource.Setup,
                GrantedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        var outcome = await WithServiceAsync(s => s.AcceptAsync(token, invitee, default));

        Assert.True(outcome.AlreadyHeld);
        Assert.Equal(InvitationState.Accepted, (await ReloadAsync(invitation.Id)).State);
        Assert.Equal(1, await WithDbAsync(db => db.CompanyAdministratorGrants.CountAsync(g => g.UserId == invitee)));
    }

    // Spec Edge Cases: an accept and a revoke at the same moment — exactly one wins, and the loser
    // is told the final state.
    [Fact]
    public async Task AcceptAndRevokeAtTheSameMoment_ExactlyOneWins()
    {
        var (companyId, senderId) = await CompanyWithAdminAsync();
        for (var round = 0; round < 5; round++)
        {
            var invitee = await WithDbAsync(CreateUserAsync);
            var invitation = await CreateAsync(companyId, senderId, EmailOf(invitee));
            var token = SingleTokenSentTo(EmailOf(invitee));

            var results = await Task.WhenAll(
                CaptureAsync(() => WithServiceAsync(s => s.AcceptAsync(token, invitee, default))),
                CaptureAsync(() => WithServiceAsync(s => s.RevokeAsync(senderId, invitation.Id, default))));

            var (acceptError, revokeError) = (results[0], results[1]);
            Assert.True((acceptError is null) ^ (revokeError is null), $"round {round}: accept={acceptError?.Code} revoke={revokeError?.Code}");
            var row = await ReloadAsync(invitation.Id);
            var granted = await WithDbAsync(db => db.CompanyAdministratorGrants.AnyAsync(g => g.UserId == invitee));
            if (acceptError is null)
            {
                Assert.Equal(InvitationState.Accepted, row.State);
                Assert.True(granted);
                Assert.Equal(("INVITATION_INVALID", "This invitation was already accepted."), (revokeError!.Code, revokeError.Message));
            }
            else
            {
                Assert.Equal(InvitationState.Revoked, row.State);
                Assert.False(granted);
                Assert.Equal("INVITATION_INVALID", acceptError.Code);
            }
        }
    }

    // FR-041c: at most five emails to one address in 24 hours, and a 60-second resend cooldown.
    [Fact]
    public async Task TheSixthEmailToOneAddressIn24Hours_IsRateLimited()
    {
        var (companyId, senderId) = await CompanyWithAdminAsync();
        var invitee = await WithDbAsync(CreateUserAsync);
        var invitation = await CreateAsync(companyId, senderId, EmailOf(invitee));
        for (var i = 0; i < 4; i++)
        {
            Clock.UtcNow = Clock.UtcNow.AddMinutes(2);
            await WithServiceAsync(s => s.ResendAsync(senderId, invitation.Id, default));
        }

        Clock.UtcNow = Clock.UtcNow.AddMinutes(2);
        var sixth = await RefusalAsync(s => s.ResendAsync(senderId, invitation.Id, default));
        Assert.Equal(("RATE_LIMITED", 429), (sixth.Code, sixth.StatusCode));
        Assert.Equal(5, TokensSentTo(EmailOf(invitee)).Count);

        // A day after the first send, the window has room again.
        Clock.UtcNow = Clock.UtcNow.AddHours(24);
        await WithServiceAsync(s => s.ResendAsync(senderId, invitation.Id, default));
        Assert.Equal(6, TokensSentTo(EmailOf(invitee)).Count);
    }

    [Fact]
    public async Task AResendInsideTheCooldown_IsRateLimited()
    {
        var (companyId, senderId) = await CompanyWithAdminAsync();
        var invitee = await WithDbAsync(CreateUserAsync);
        var invitation = await CreateAsync(companyId, senderId, EmailOf(invitee));

        Clock.UtcNow = Clock.UtcNow.AddSeconds(59);
        var refused = await RefusalAsync(s => s.ResendAsync(senderId, invitation.Id, default));

        Assert.Equal(("RATE_LIMITED", 429), (refused.Code, refused.StatusCode));
        Assert.Single(TokensSentTo(EmailOf(invitee)));
    }

    // FR-041c: the activation fan-out sends even to an address that has used up its cap.
    [Fact]
    public async Task TheActivationFanOut_BypassesTheRecipientCap()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        var property = await CreatePropertyAsync(community);
        var (managerId, _) = await CreateManagerAsync(community);
        var email = $"capped-{Guid.NewGuid():N}@example.com";
        var scheduledId = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.Invitations.Add(new Invitation
            {
                Id = scheduledId, Kind = InvitationKind.Residency, CompanyId = companyId, CommunityId = community,
                PropertyId = property, Email = email, NormalizedEmail = email, State = InvitationState.Scheduled,
                SenderUserId = managerId, CreatedAt = Clock.UtcNow.AddDays(-1),
            });
            for (var i = 0; i < 5; i++)
                db.InvitationDeliveries.Add(new InvitationDelivery
                {
                    Id = Guid.NewGuid(), InvitationId = scheduledId, NormalizedEmail = email,
                    AttemptedAt = Clock.UtcNow.AddHours(-1), Succeeded = true,
                });
            await db.SaveChangesAsync();
        });

        var report = await WithScopeAsync(async sp =>
        {
            var db = Db(sp);
            var service = sp.GetRequiredService<InvitationService>();
            var release = await CommunityLocks.RunAsync(db, community, async () =>
            {
                var r = await service.ReleaseScheduledAsync(community, managerId, "CommunityActivated", default);
                await db.SaveChangesAsync();
                return r;
            }, default);
            return await service.DeliverAsync(release.Sends, default);
        });

        Assert.Equal(new DeliveryReport(1, 0), report);
        var row = await ReloadAsync(scheduledId);
        Assert.Equal(InvitationState.Pending, row.State);
        Assert.Equal(Clock.UtcNow.AddHours(72), row.ExpiresAt);
        Assert.Single(TokensSentTo(email));
        Assert.True((await DeliveriesAsync(scheduledId)).Single(d => d.AttemptedAt == Clock.UtcNow).BypassesRecipientCap);
    }

    [Fact]
    public async Task ANewInvitation_AfterAnExpiredOne_SupersedesIt()
    {
        var (companyId, senderId) = await CompanyWithAdminAsync();
        var invitee = await WithDbAsync(CreateUserAsync);
        var first = await CreateAsync(companyId, senderId, EmailOf(invitee));

        // While it is open and unexpired, a second invitation is refused with the open one's id.
        var duplicate = await RefusalAsync(s => s.CreateAsync(Request(companyId, EmailOf(invitee)), senderId, default));
        Assert.Equal(("INVITATION_PENDING", 409), (duplicate.Code, duplicate.StatusCode));
        Assert.Equal(first.Id, duplicate.Extensions!["invitationId"]);

        Clock.UtcNow = Clock.UtcNow.AddHours(73);
        var second = await CreateAsync(companyId, senderId, EmailOf(invitee));

        var old = await ReloadAsync(first.Id);
        Assert.Equal((InvitationState.Revoked, InvitationRevokeReason.Superseded), (old.State, old.RevokeReason));
        Assert.Equal(InvitationState.Pending, (await ReloadAsync(second.Id)).State);
    }

    // FR-023, FR-040: one InvitationChange per change and one InvitationAcceptFailed per refusal,
    // and no event carries the token, an email address or a name.
    [Fact]
    public async Task EveryChangeAndRefusal_IsLoggedOnce_WithoutTokenEmailOrName()
    {
        var (companyId, senderId) = await CompanyWithAdminAsync();
        var invitee = await WithDbAsync(CreateUserAsync);
        var bob = await WithDbAsync(CreateUserAsync);
        var invitation = await CreateAsync(companyId, senderId, EmailOf(invitee));
        Clock.UtcNow = Clock.UtcNow.AddMinutes(2);
        await WithServiceAsync(s => s.ResendAsync(senderId, invitation.Id, default));
        var token = TokensSentTo(EmailOf(invitee)).Last();
        await RefusalAsync(s => s.AcceptAsync(token, bob, default));
        await WithServiceAsync(s => s.AcceptAsync(token, invitee, default));

        var events = LogSink.Events
            .Where(e => e.Properties.TryGetValue("InvitationId", out var v) && v.ToString().Trim('"') == invitation.Id.ToString())
            .ToList();
        var changes = events.Where(e => e.MessageTemplate.Text.StartsWith("InvitationChange"))
            .Select(e => e.Properties["Change"].ToString().Trim('"')).ToList();
        Assert.Equal(["Created", "Resent", "Accepted"], changes);
        var refusal = Assert.Single(events, e => e.MessageTemplate.Text.StartsWith("InvitationAcceptFailed"));
        Assert.Equal("\"INVITATION_EMAIL_MISMATCH\"", refusal.Properties["Reason"].ToString());

        var rendered = string.Join("\n", events.Select(e =>
            e.RenderMessage() + " " + string.Join(" ", e.Properties.Values.Select(v => v.ToString()))));
        foreach (var secret in TokensSentTo(EmailOf(invitee)).Append(EmailOf(invitee)).Append("Board Tester"))
            Assert.DoesNotContain(secret, rendered, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(Guid CompanyId, string SenderId)> CompanyWithAdminAsync()
    {
        var companyId = await CreateCompanyAsync();
        var (senderId, _) = await CreateCompanyAdminAsync(companyId);
        return (companyId, senderId);
    }

    private static NewInvitation Request(Guid companyId, string email) =>
        new(InvitationKind.CompanyAdministrator, companyId, null, null, null, null, email);

    private Task<Invitation> CreateAsync(Guid companyId, string senderId, string email) =>
        WithServiceAsync(s => s.CreateAsync(Request(companyId, email), senderId, default));

    // FR-018: the service refuses a membership role that isn't offered, whatever the caller, and writes nothing.
    [Theory]
    [InlineData(CommunityRole.Resident)]
    [InlineData(null)]
    public async Task AMembershipInvitationForARoleThatIsntOffered_IsRefused_AndNothingIsWritten(CommunityRole? role)
    {
        var companyId = await CreateCompanyAsync();
        var (senderId, _) = await CreateCompanyAdminAsync(companyId);
        var community = await CreateCommunityInAsync(companyId);
        var email = $"role-{Guid.NewGuid():N}@example.com";

        var refused = await RefusalAsync(s => s.CreateAsync(
            new NewInvitation(InvitationKind.Membership, companyId, community, null, role, null, email), senderId, default));

        Assert.Equal(("VALIDATION_ERROR", 422, "Choose Community Manager, Board Member or Accountant."),
            (refused.Code, refused.StatusCode, refused.Message));
        Assert.False(await WithDbAsync(db => db.Invitations.AnyAsync(i => i.CompanyId == companyId)));
        Assert.DoesNotContain(Email.Sent, m => m.Target == email);
    }

    private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> f)
    {
        using var scope = NewScope();
        return await f(scope.ServiceProvider);
    }

    private static Infrastructure.Persistence.ApplicationDbContext Db(IServiceProvider sp) =>
        sp.GetRequiredService<Infrastructure.Persistence.ApplicationDbContext>();

    private Task<T> WithServiceAsync<T>(Func<InvitationService, Task<T>> f) =>
        WithScopeAsync(sp => f(sp.GetRequiredService<InvitationService>()));

    private async Task<DomainException> RefusalAsync<T>(Func<InvitationService, Task<T>> f) =>
        await Assert.ThrowsAsync<DomainException>(() => WithServiceAsync(f));

    private static async Task<DomainException?> CaptureAsync<T>(Func<Task<T>> f)
    {
        try { await f(); return null; }
        catch (DomainException ex) { return ex; }
    }

    private Task<Invitation> ReloadAsync(Guid id) =>
        WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(i => i.Id == id));

    private Task<List<InvitationDelivery>> DeliveriesAsync(Guid invitationId) =>
        WithDbAsync(db => db.InvitationDeliveries.AsNoTracking().Where(d => d.InvitationId == invitationId).ToListAsync());

    private Task RevokeGrantAsync(string userId) => WithDbAsync<int>(db => db.CompanyAdministratorGrants
        .Where(g => g.UserId == userId && g.RevokedAt == null)
        .ExecuteUpdateAsync(s => s.SetProperty(g => g.RevokedAt, DateTimeOffset.UtcNow)));

    private List<string> TokensSentTo(string email) =>
        Email.Sent.Where(m => m.Target == email).Select(m => LinkToken().Match(m.Body).Groups[1].Value).ToList();

    private string SingleTokenSentTo(string email) => Assert.Single(TokensSentTo(email));
}
