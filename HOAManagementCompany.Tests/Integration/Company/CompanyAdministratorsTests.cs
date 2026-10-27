using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T133 — granting and revoking the Company Administrator role (US7; FR-037, FR-039;
/// contracts/company.md § Company Administrators).
/// </summary>
public partial class CompanyAdministratorsTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [GeneratedRegex("/invite#t=([A-Za-z0-9_-]{43})")]
    private static partial Regex LinkToken();

    // US7-S1
    [Fact]
    public async Task AnAdministratorInvitesOps_WhoRegistersAndCanOpenTheCompanyArea()
    {
        var companyId = await CreateCompanyAsync();
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        var ops = $"ops-{Guid.NewGuid():N}@example.com";

        var res = await admin.PostAsJsonAsync("/api/v1/company/administrators/invitations", new { email = ops });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal(("Pending", Clock.UtcNow.AddHours(72)),
            (body.GetProperty("state").GetString(), body.GetProperty("expiresAt").GetDateTimeOffset()));
        var invitation = await WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(i => i.Id == body.GetProperty("id").GetGuid()));
        Assert.Equal((InvitationKind.CompanyAdministrator, companyId), (invitation.Kind, invitation.CompanyId));
        var token = TokensSentTo(ops).Single();

        var register = await CreateClient().PostAsJsonAsync("/api/v1/invitations/register",
            new { token, firstName = "Ops", lastName = "Lead", password = "Str0ng!Pass" });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        using var opsClient = ClientWithToken((await JsonAsync(register)).GetProperty("token").GetString()!);
        var me = await JsonAsync(await opsClient.GetAsync("/api/v1/auth/me"));
        Assert.True(me.GetProperty("isCompanyAdministrator").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await opsClient.GetAsync("/api/v1/company/communities")).StatusCode);
    }

    // US7-S2
    [Fact]
    public async Task RevokingAnotherAdministrator_EndsTheirCompanyAccess_AndTheInvitationsOnlyTheyCouldSend()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, firstEmail) = await CreateCompanyAdminAsync(companyId);
        var (secondId, secondEmail) = await CreateCompanyAdminAsync(companyId);
        await WithDbAsync(async db =>
        {
            db.CommunityMemberships.Add(new CommunityMembership
            {
                Id = Guid.NewGuid(), CommunityId = community, UserId = secondId, Role = CommunityRole.CommunityManager,
                Status = MembershipStatus.Active, StartDate = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime).AddDays(-30),
            });
            await db.SaveChangesAsync();
        });
        using var first = await ClientForAsync(firstEmail);
        using var second = await ClientForAsync(secondEmail);
        var adminInvite = (await JsonAsync(await second.PostAsJsonAsync("/api/v1/company/administrators/invitations",
            new { email = $"ops-{Guid.NewGuid():N}@example.com" }))).GetProperty("id").GetGuid();
        var memberInvite = (await JsonAsync(await second.PostAsJsonAsync($"/api/v1/communities/{community}/memberships",
            new { role = (int)CommunityRole.BoardMember, email = $"lee-{Guid.NewGuid():N}@example.com" }))).GetProperty("invitationId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/api/v1/company/communities")).StatusCode);

        var res = await first.PostAsync($"/api/v1/company/administrators/{secondId}/revoke", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal((true, 1), (body.GetProperty("revoked").GetBoolean(), body.GetProperty("invitationsRevoked").GetInt32()));
        var next = await second.GetAsync("/api/v1/company/communities");
        Assert.Equal(HttpStatusCode.Forbidden, next.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(next)).GetProperty("code").GetString());
        var invitations = await WithDbAsync(db => db.Invitations.AsNoTracking()
            .Where(i => i.Id == adminInvite || i.Id == memberInvite).ToDictionaryAsync(i => i.Id));
        Assert.Equal((InvitationState.Revoked, (InvitationRevokeReason?)InvitationRevokeReason.SenderLostAuthority),
            (invitations[adminInvite].State, invitations[adminInvite].RevokeReason));
        Assert.Equal(InvitationState.Pending, invitations[memberInvite].State);
        var grant = await WithDbAsync(db => db.CompanyAdministratorGrants.AsNoTracking().SingleAsync(g => g.UserId == secondId));
        Assert.NotNull(grant.RevokedAt);
    }

    // US7-S3
    [Fact]
    public async Task TheOnlyAdministrator_CantRevokeThemself()
    {
        var companyId = await CreateCompanyAsync();
        var (adminId, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PostAsync($"/api/v1/company/administrators/{adminId}/revoke", null);

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        Assert.Equal("LAST_COMPANY_ADMIN", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.Null(await WithDbAsync(db => db.CompanyAdministratorGrants.Where(g => g.UserId == adminId).Select(g => g.RevokedAt).SingleAsync()));
    }

    // US7-S5
    [Fact]
    public async Task ACommunityManager_CantListInviteOrRevokeAdministrators()
    {
        var companyId = await CreateCompanyAsync();
        var (adminId, _) = await CreateCompanyAdminAsync(companyId);
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);

        var responses = new[]
        {
            await manager.GetAsync("/api/v1/company/administrators"),
            await manager.GetAsync("/api/v1/company/invitations"),
            await manager.PostAsJsonAsync("/api/v1/company/administrators/invitations", new { email = "ops@example.com" }),
            await manager.PostAsync($"/api/v1/company/administrators/{adminId}/revoke", null),
        };

        foreach (var res in responses)
        {
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        }
        Assert.False(await WithDbAsync(db => db.Invitations.AnyAsync(i => i.CompanyId == companyId)));
    }

    // US7-S10
    [Fact]
    public async Task AnAdministrator_CantGrantThemselfABoardSeat_OrInviteThemself()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        await CreateManagerAsync(community);
        var (adminId, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var seat = await admin.PostAsJsonAsync($"/api/v1/communities/{community}/memberships",
            new { role = (int)CommunityRole.BoardMember, userId = adminId });
        var self = await admin.PostAsJsonAsync("/api/v1/company/administrators/invitations", new { email = adminEmail.ToUpperInvariant() });

        foreach (var res in new[] { seat, self })
        {
            Assert.Equal((HttpStatusCode)422, res.StatusCode);
            Assert.Equal("SELF_GRANT_NOT_ALLOWED", (await JsonAsync(res)).GetProperty("code").GetString());
        }
        Assert.False(await WithDbAsync(db => db.CommunityMemberships.AnyAsync(m => m.UserId == adminId)));
        Assert.False(await WithDbAsync(db => db.Invitations.AnyAsync(i => i.CompanyId == companyId)));
    }

    // US7-S11
    [Fact]
    public async Task EachGrantRevokeAndInvitation_LogsOneCompanyAdminChange_WithoutEmails()
    {
        var companyId = await CreateCompanyAsync();
        var (adminId, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        var opsId = await WithDbAsync(CreateUserAsync);

        // Each action happens at its own time, so each event's time is checked against its own action.
        Clock.UtcNow = Clock.UtcNow.AddMinutes(1);
        var invitedAt = Clock.UtcNow;
        var invitationId = (await JsonAsync(await admin.PostAsJsonAsync("/api/v1/company/administrators/invitations",
            new { email = EmailOf(opsId) }))).GetProperty("id").GetGuid();
        Clock.UtcNow = Clock.UtcNow.AddMinutes(2);
        var grantedAt = Clock.UtcNow;
        using (var ops = await ClientForAsync(EmailOf(opsId)))
            Assert.Equal(HttpStatusCode.OK,
                (await ops.PostAsJsonAsync("/api/v1/invitations/accept", new { token = TokensSentTo(EmailOf(opsId)).Single() })).StatusCode);
        Clock.UtcNow = Clock.UtcNow.AddMinutes(3);
        var revokedAt = Clock.UtcNow;
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/company/administrators/{opsId}/revoke", null)).StatusCode);

        var events = LogSink.Events.Where(e => e.MessageTemplate.Text.StartsWith("CompanyAdminChange")
                                               && e.Properties["CompanyId"].ToString() == companyId.ToString()).ToList();
        Assert.Equal([("Invited", adminId, "null", invitationId.ToString(), invitedAt),
                ("Granted", opsId, opsId, invitationId.ToString(), grantedAt), ("Revoked", adminId, opsId, "null", revokedAt)],
            events.Select(e => (Prop(e, "Change"), Prop(e, "ActorId"), Prop(e, "TargetUserId"), Prop(e, "InvitationId"), At(e))));
        // UTC time: the same instant at a local offset would compare equal above.
        Assert.All(events, e => Assert.Equal(TimeSpan.Zero, At(e).Offset));
        var rendered = string.Join("\n", events.Select(e => e.RenderMessage() + " " + string.Join(" ", e.Properties.Values)));
        Assert.DoesNotContain("@", rendered);
    }

    [Fact]
    public async Task TheListsPage_AndFollowTheContract()
    {
        var companyId = await CreateCompanyAsync();
        var (adminId, adminEmail) = await CreateCompanyAdminAsync(companyId);
        for (var i = 0; i < 26; i++)
            await CreateCompanyAdminAsync(companyId);
        await WithDbAsync(async db =>
        {
            for (var i = 0; i < 27; i++)
                db.Invitations.Add(new Invitation
                {
                    Id = Guid.NewGuid(), Kind = InvitationKind.CompanyAdministrator, CompanyId = companyId,
                    Email = $"ops{i}@example.com", NormalizedEmail = $"ops{i}@example.com", State = InvitationState.Pending,
                    SenderUserId = adminId, CreatedAt = Clock.UtcNow.AddMinutes(-i), SentAt = Clock.UtcNow.AddMinutes(-i),
                    ExpiresAt = Clock.UtcNow.AddMinutes(-i).AddHours(72), TokenHash = Guid.NewGuid().ToString("N"),
                });
            await db.SaveChangesAsync();
        });
        using var admin = await ClientForAsync(adminEmail);

        var page = await JsonAsync(await admin.GetAsync("/api/v1/company/administrators"));
        Assert.Equal((27, 25, 0, 25), (page.GetProperty("total").GetInt32(), page.GetProperty("limit").GetInt32(),
            page.GetProperty("offset").GetInt32(), page.GetProperty("items").GetArrayLength()));
        var item = page.GetProperty("items")[0];
        foreach (var field in new[] { "userId", "name", "email", "grantedAt", "grantedBy", "source" })
            Assert.True(item.TryGetProperty(field, out _), field);
        var rest = await JsonAsync(await admin.GetAsync("/api/v1/company/administrators?offset=25&limit=101"));
        Assert.Equal((100, 2), (rest.GetProperty("limit").GetInt32(), rest.GetProperty("items").GetArrayLength()));

        var invitations = await JsonAsync(await admin.GetAsync("/api/v1/company/invitations?limit=101"));
        Assert.Equal((27, 100, 27), (invitations.GetProperty("total").GetInt32(), invitations.GetProperty("limit").GetInt32(),
            invitations.GetProperty("items").GetArrayLength()));
        var first = invitations.GetProperty("items")[0];
        Assert.Equal("ops0@example.com", first.GetProperty("email").GetString());
        foreach (var field in new[] { "id", "email", "state", "effectiveState", "revokeReason", "sentBy", "sentAt", "expiresAt", "canResend", "canRevoke" })
            Assert.True(first.TryGetProperty(field, out _), field);
        Assert.Equal(25, (await JsonAsync(await admin.GetAsync("/api/v1/company/invitations"))).GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task InvitingACurrentAdministrator_IsAlreadyMember_AndADuplicateIsPending()
    {
        var companyId = await CreateCompanyAsync();
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        var (_, otherEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        var ops = $"ops-{Guid.NewGuid():N}@example.com";

        var current = await admin.PostAsJsonAsync("/api/v1/company/administrators/invitations", new { email = otherEmail });
        var firstId = (await JsonAsync(await admin.PostAsJsonAsync("/api/v1/company/administrators/invitations", new { email = ops })))
            .GetProperty("id").GetGuid();
        var duplicate = await admin.PostAsJsonAsync("/api/v1/company/administrators/invitations", new { email = ops });

        Assert.Equal(HttpStatusCode.Conflict, current.StatusCode);
        Assert.Equal("ALREADY_MEMBER", (await JsonAsync(current)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var body = await JsonAsync(duplicate);
        Assert.Equal(("INVITATION_PENDING", firstId), (body.GetProperty("code").GetString(), body.GetProperty("invitationId").GetGuid()));
    }

    [Fact]
    public async Task TheCompanyInvitationRoutes_ResendAndRevoke()
    {
        var companyId = await CreateCompanyAsync();
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        var ops = $"ops-{Guid.NewGuid():N}@example.com";
        var id = (await JsonAsync(await admin.PostAsJsonAsync("/api/v1/company/administrators/invitations", new { email = ops })))
            .GetProperty("id").GetGuid();
        var oldToken = TokensSentTo(ops).Single();
        Clock.UtcNow = Clock.UtcNow.AddMinutes(2);

        var resend = await admin.PostAsync($"/api/v1/company/invitations/{id}/resend", null);

        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        Assert.Equal(Clock.UtcNow.AddHours(72), (await JsonAsync(resend)).GetProperty("expiresAt").GetDateTimeOffset());
        Assert.NotEqual(oldToken, TokensSentTo(ops).Last());

        var revoke = await admin.PostAsync($"/api/v1/company/invitations/{id}/revoke", null);

        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        var item = await JsonAsync(revoke);
        Assert.Equal(("Revoked", "Manual"), (item.GetProperty("state").GetString(), item.GetProperty("revokeReason").GetString()));
        var again = await admin.PostAsync($"/api/v1/company/invitations/{id}/revoke", null);
        Assert.Equal(HttpStatusCode.Gone, again.StatusCode);
        Assert.Equal("INVITATION_INVALID", (await JsonAsync(again)).GetProperty("code").GetString());

        // Another company's invitation looks like no access at all.
        var (_, otherAdmin) = await CreateCompanyAdminAsync(await CreateCompanyAsync());
        using var other = await ClientForAsync(otherAdmin);
        var foreign = await other.PostAsync($"/api/v1/company/invitations/{id}/resend", null);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }

    // contracts/company.md § GET /company/invitations: the state filter, by name, case-insensitive.
    [Fact]
    public async Task TheInvitationsList_FiltersByState_AndRefusesAnUnknownState()
    {
        var companyId = await CreateCompanyAsync();
        var (adminId, adminEmail) = await CreateCompanyAdminAsync(companyId);
        var ids = new Dictionary<InvitationState, Guid>();
        await WithDbAsync(async db =>
        {
            foreach (var state in new[] { InvitationState.Pending, InvitationState.Revoked, InvitationState.Accepted })
            {
                var invitation = new Invitation
                {
                    Id = Guid.NewGuid(), Kind = InvitationKind.CompanyAdministrator, CompanyId = companyId,
                    Email = $"{state}@example.com", NormalizedEmail = $"{state}@example.com".ToLowerInvariant(), State = state,
                    SenderUserId = adminId, CreatedAt = Clock.UtcNow, SentAt = Clock.UtcNow, ExpiresAt = Clock.UtcNow.AddHours(72),
                };
                db.Invitations.Add(invitation);
                ids[state] = invitation.Id;
            }
            await db.SaveChangesAsync();
        });
        using var admin = await ClientForAsync(adminEmail);

        var pending = await JsonAsync(await admin.GetAsync("/api/v1/company/invitations?state=pending"));
        var revoked = await JsonAsync(await admin.GetAsync("/api/v1/company/invitations?state=Revoked"));
        var all = await JsonAsync(await admin.GetAsync("/api/v1/company/invitations"));

        Assert.Equal([ids[InvitationState.Pending]], Ids(pending));
        Assert.Equal([ids[InvitationState.Revoked]], Ids(revoked));
        Assert.Equal(new[] { ids[InvitationState.Pending], ids[InvitationState.Revoked] }.Order(), Ids(all).Order());
        foreach (var unknown in new[] { "expired", "1", "everything" })
        {
            var res = await admin.GetAsync($"/api/v1/company/invitations?state={unknown}");
            Assert.Equal((HttpStatusCode)422, res.StatusCode);
            var body = await JsonAsync(res);
            Assert.Equal("VALIDATION_ERROR", body.GetProperty("code").GetString());
            var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
            Assert.Equal(("state", "state must be pending, scheduled or revoked."),
                (error.GetProperty("field").GetString(), error.GetProperty("message").GetString()));
        }
    }

    // contracts/company.md § POST /company/administrators/invitations: 422 VALIDATION_ERROR on email.
    [Theory]
    [InlineData("not-an-email")]
    [InlineData("Ops Lead <ops@example.com>")]
    [InlineData("   ")]
    [InlineData("LONG")]
    public async Task InvitingAnInvalidEmail_Is422_OnEmail_AndNothingIsSent(string email)
    {
        var companyId = await CreateCompanyAsync();
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        if (email == "LONG")
            email = new string('a', Invitation.MaxEmailLength - "@example.com".Length + 1) + "@example.com";

        var res = await admin.PostAsJsonAsync("/api/v1/company/administrators/invitations", new { email });

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("code").GetString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal(("email", "Enter a valid email address."),
            (error.GetProperty("field").GetString(), error.GetProperty("message").GetString()));
        Assert.False(await WithDbAsync(db => db.Invitations.AnyAsync(i => i.CompanyId == companyId)));
        Assert.Empty(Email.Sent);
    }

    // US7-S5: the company invitation routes are Company Administrators only.
    [Fact]
    public async Task ACommunityManager_CantResendOrRevokeACompanyAdministratorInvitation()
    {
        var companyId = await CreateCompanyAsync();
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var admin = await ClientForAsync(adminEmail);
        using var manager = await ClientForAsync(managerEmail);
        var ops = $"ops-{Guid.NewGuid():N}@example.com";
        var id = (await JsonAsync(await admin.PostAsJsonAsync("/api/v1/company/administrators/invitations", new { email = ops })))
            .GetProperty("id").GetGuid();
        Clock.UtcNow = Clock.UtcNow.AddMinutes(2);

        foreach (var action in new[] { "resend", "revoke" })
        {
            var res = await manager.PostAsync($"/api/v1/company/invitations/{id}/{action}", null);
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        }
        var invitation = await WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(i => i.Id == id));
        Assert.Equal((InvitationState.Pending, 0), (invitation.State, invitation.ResendCount));
        Assert.Single(TokensSentTo(ops));
    }

    private static List<Guid> Ids(JsonElement list) =>
        list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();

    private List<string> TokensSentTo(string email) =>
        Email.Sent.Where(m => m.Target == email).Select(m => LinkToken().Match(m.Body))
            .Where(m => m.Success).Select(m => m.Groups[1].Value).ToList();

    private static string? Prop(Serilog.Events.LogEvent e, string name) =>
        e.Properties.TryGetValue(name, out var v) ? v.ToString().Trim('"') : null;

    private static DateTimeOffset At(Serilog.Events.LogEvent e) =>
        (DateTimeOffset)((Serilog.Events.ScalarValue)e.Properties["UtcNow"]).Value!;
}
