using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T077 — appointing managers and members, directly or by invitation, and the one-action
/// replace (US2; FR-018–FR-021; contracts/community-settings.md § Managers and memberships).
/// </summary>
public partial class ManagerAppointmentTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private readonly FailEndingAManager _failEnd = new();

    [GeneratedRegex("/invite#t=([A-Za-z0-9_-]{43})")]
    private static partial Regex LinkToken();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        services.ConfigureDbContext<ApplicationDbContext>(o => o.AddInterceptors(_failEnd));
    }

    // US2-S1 (send)
    [Fact]
    public async Task CompanyAdministrator_AppointsAManagerByEmail_SendsAPendingInvitation()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        var pat = NewEmail("pat");

        var res = await admin.PostAsJsonAsync(Url(community), new { role = (int)CommunityRole.CommunityManager, email = pat });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await JsonAsync(res);
        var invitationId = body.GetProperty("invitationId").GetGuid();
        Assert.Equal("Pending", body.GetProperty("state").GetString());
        Assert.Equal(Clock.UtcNow.AddHours(72), body.GetProperty("expiresAt").GetDateTimeOffset());
        var sent = Assert.Single(Email.Sent, m => m.Target == pat);
        Assert.Matches(LinkToken(), sent.Body);

        var list = await JsonAsync(await admin.GetAsync($"/api/v1/communities/{community}/invitations"));
        var item = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal((invitationId, pat, "CommunityManager", "Pending"),
            (item.GetProperty("id").GetGuid(), item.GetProperty("email").GetString(), item.GetProperty("role").GetString(),
             item.GetProperty("effectiveState").GetString()));
    }

    // contracts/community-settings.md: the settings page sends the role by name.
    [Theory]
    [InlineData("CommunityManager")]
    [InlineData("BoardMember")]
    [InlineData("Accountant")]
    public async Task ARoleSentByName_IsAccepted(string role)
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        var email = NewEmail("named");

        var res = await admin.PostAsJsonAsync(Url(community), new { role, email });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var list = await JsonAsync(await admin.GetAsync($"/api/v1/communities/{community}/invitations"));
        var item = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal((email, role), (item.GetProperty("email").GetString(), item.GetProperty("role").GetString()));
    }

    // US2-S3
    [Fact]
    public async Task CommunityManager_AppointsABoardMemberFromThePeopleList_AsAnOpenEndedCoManager()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        var (leeId, _) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        using var manager = await ClientForAsync(managerEmail);

        var people = await JsonAsync(await manager.GetAsync($"/api/v1/communities/{community}/people"));
        var lee = Assert.Single(people.GetProperty("items").EnumerateArray(), p => p.GetProperty("userId").GetString() == leeId);
        Assert.Equal(["BoardMember"], lee.GetProperty("rolesHere").EnumerateArray().Select(r => r.GetString()));

        var res = await manager.PostAsJsonAsync(Url(community), new { role = (int)CommunityRole.CommunityManager, userId = leeId });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var dto = await JsonAsync(res);
        Assert.Equal((leeId, "CommunityManager", "Active", JsonValueKind.Null),
            (dto.GetProperty("userId").GetString(), dto.GetProperty("role").GetString(), dto.GetProperty("status").GetString(),
             dto.GetProperty("endDate").ValueKind));
        Assert.False(await WithDbAsync(db => db.Invitations.AnyAsync(i => i.CommunityId == community)));
        Assert.Equal(2, await WithDbAsync(db => db.CommunityMemberships.CountAsync(m => m.CommunityId == community
            && m.Role == CommunityRole.CommunityManager && m.Status == MembershipStatus.Active && m.EndDate == null)));
    }

    // US2-S4: the response says nothing about whether the email has an account.
    [Fact]
    public async Task AppointingByEmail_GivesTheSameResponse_WithOrWithoutAnAccount()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        var withAccount = EmailOf(await WithDbAsync(CreateUserAsync));
        var withoutAccount = NewEmail("no-account");
        using var manager = await ClientForAsync(managerEmail);

        var a = await manager.PostAsJsonAsync(Url(community), new { role = (int)CommunityRole.CommunityManager, email = withAccount });
        var b = await manager.PostAsJsonAsync(Url(community), new { role = (int)CommunityRole.CommunityManager, email = withoutAccount });

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (a.StatusCode, b.StatusCode));
        var (bodyA, bodyB) = (await JsonAsync(a), await JsonAsync(b));
        Assert.Equal(["expiresAt", "invitationId", "state"], bodyA.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(bodyA.EnumerateObject().Select(p => p.Name).Order(), bodyB.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(("Pending", "Pending"), (bodyA.GetProperty("state").GetString(), bodyB.GetProperty("state").GetString()));
        Assert.Equal(bodyA.GetProperty("expiresAt").GetDateTimeOffset(), bodyB.GetProperty("expiresAt").GetDateTimeOffset());
        Assert.DoesNotContain("Board Tester", bodyA.GetRawText());
        Assert.Single(Email.Sent, m => m.Target == withAccount);
        Assert.Single(Email.Sent, m => m.Target == withoutAccount);
    }

    // US2-S8
    [Fact]
    public async Task ManagerOfA_AppointingInB_IsForbiddenLikeANonexistentCommunity()
    {
        var companyId = await CreateCompanyAsync();
        var a = await CreateCommunityInAsync(companyId);
        var b = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(a);
        var (targetId, _) = await CreateStaffAsync(b, CommunityRole.BoardMember);
        using var manager = await ClientForAsync(managerEmail);
        var body = new { role = (int)CommunityRole.CommunityManager, userId = targetId };

        var inB = await manager.PostAsJsonAsync(Url(b), body);
        var missing = await manager.PostAsJsonAsync(Url(Guid.NewGuid()), body);

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (inB.StatusCode, missing.StatusCode));
        Assert.Equal("FORBIDDEN", (await JsonAsync(inB)).GetProperty("code").GetString());
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await inB.Content.ReadAsStringAsync());
        Assert.False(await WithDbAsync(db => db.CommunityMemberships.AnyAsync(m => m.CommunityId == b && m.UserId == targetId
                                                                              && m.Role == CommunityRole.CommunityManager)));
    }

    // US2-S11
    [Fact]
    public async Task CompanyAdministrator_ReplacesTheLastManager_InOneAction()
    {
        var (companyId, community, patId, leeId, adminEmail) = await ReplaceSetupAsync();
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PostAsJsonAsync(ReplaceUrl(community), new { currentManagerUserId = patId, replacementUserId = leeId });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal((patId, "Inactive"), (body.GetProperty("ended").GetProperty("userId").GetString(),
            body.GetProperty("ended").GetProperty("status").GetString()));
        Assert.Equal((leeId, "CommunityManager", JsonValueKind.Null), (body.GetProperty("appointed").GetProperty("userId").GetString(),
            body.GetProperty("appointed").GetProperty("role").GetString(), body.GetProperty("appointed").GetProperty("endDate").ValueKind));
        var (pat, lee) = await ManagerRowsAsync(community, patId, leeId);
        Assert.Equal((MembershipStatus.Inactive, (DateOnly?)Today()), (pat!.Status, pat.EndDate));
        Assert.Equal((MembershipStatus.Active, (DateOnly?)null), (lee!.Status, lee.EndDate));

        var events = LogSink.Events.Where(e => e.MessageTemplate.Text.StartsWith("BoardMembershipChange")
                                                && e.Properties["CommunityId"].ToString() == community.ToString()).ToList();
        var grant = Assert.Single(events, e => e.Properties["Kind"].ToString() == "\"replace-grant\"");
        var end = Assert.Single(events, e => e.Properties["Kind"].ToString() == "\"replace-end\"");
        Assert.Equal(grant.Properties["Cause"].ToString(), end.Properties["Cause"].ToString());
        Assert.StartsWith("\"replace:", grant.Properties["Cause"].ToString());
    }

    // US2-S11: the grant and the end are one transaction.
    [Fact]
    public async Task Replace_FailingAfterTheGrant_LeavesBothManagersUnchanged()
    {
        var (_, community, patId, leeId, adminEmail) = await ReplaceSetupAsync();
        using var admin = await ClientForAsync(adminEmail);

        _failEnd.Armed = true;
        var res = await admin.PostAsJsonAsync(ReplaceUrl(community), new { currentManagerUserId = patId, replacementUserId = leeId });

        Assert.False(_failEnd.Armed, "the failure was never injected");
        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);
        var (pat, lee) = await ManagerRowsAsync(community, patId, leeId);
        Assert.Equal((MembershipStatus.Active, (DateOnly?)null), (pat!.Status, pat.EndDate));
        Assert.Null(lee);
    }

    // US2-S11: the replacement must already be in the community, and only a Company Administrator replaces.
    [Fact]
    public async Task Replace_WithAnOutsider_Is422_AndByAManager_Is403()
    {
        var (companyId, community, patId, _, adminEmail) = await ReplaceSetupAsync();
        var outsider = await WithDbAsync(CreateUserAsync);
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PostAsJsonAsync(ReplaceUrl(community), new { currentManagerUserId = patId, replacementUserId = outsider });

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("code").GetString());
        Assert.Equal("Choose someone from this community, or invite them by email.", body.GetProperty("message").GetString());
        var (pat, _) = await ManagerRowsAsync(community, patId, outsider);
        Assert.Equal(MembershipStatus.Active, pat!.Status);

        var patEmail = EmailOf(patId);
        using var manager = await ClientForAsync(patEmail);
        var (coId, _) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        var byManager = await manager.PostAsJsonAsync(ReplaceUrl(community), new { currentManagerUserId = patId, replacementUserId = coId });
        Assert.Equal(HttpStatusCode.Forbidden, byManager.StatusCode);
        Assert.Equal("FORBIDDEN", (await JsonAsync(byManager)).GetProperty("code").GetString());
    }

    // US2-S16 (send)
    [Fact]
    public async Task CommunityManager_InvitesABoardMemberWithATermEnd()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);
        var invited = NewEmail("new");

        var res = await manager.PostAsJsonAsync(Url(community),
            new { role = (int)CommunityRole.BoardMember, email = invited, endDate = "2027-12-31" });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var invitation = await WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(i => i.CommunityId == community));
        Assert.Equal((InvitationKind.Membership, CommunityRole.BoardMember, (DateOnly?)new DateOnly(2027, 12, 31), InvitationState.Pending),
            (invitation.Kind, invitation.Role, invitation.TermEndDate, invitation.State));
    }

    // US2-S17: nobody grants themself a role, directly or by email.
    [Theory]
    [InlineData("userId")]
    [InlineData("email")]
    public async Task AManager_AppointingThemself_IsRefused_AndNothingIsCreated(string path)
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (managerId, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);
        object body = path == "userId"
            ? new { role = (int)CommunityRole.BoardMember, userId = managerId }
            : new { role = (int)CommunityRole.BoardMember, email = "  " + managerEmail.ToUpperInvariant() + " " };

        var res = await manager.PostAsJsonAsync(Url(community), body);

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        Assert.Equal("SELF_GRANT_NOT_ALLOWED", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.False(await WithDbAsync(db => db.CommunityMemberships.AnyAsync(m => m.CommunityId == community
                                                                              && m.Role == CommunityRole.BoardMember)));
        Assert.False(await WithDbAsync(db => db.Invitations.AnyAsync(i => i.CommunityId == community)));
    }

    // US2-S18: only managers and Company Administrators appoint or end managers.
    [Theory]
    [InlineData(CommunityRole.BoardMember)]
    [InlineData(CommunityRole.Accountant)]
    [InlineData(CommunityRole.Resident)]
    public async Task OtherRoles_CantCreateOrEndAManager(CommunityRole role)
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (managerId, _) = await CreateManagerAsync(community);
        var managerRow = await WithDbAsync(db => db.CommunityMemberships.Where(m => m.UserId == managerId).Select(m => m.Id).SingleAsync());
        var (targetId, _) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        var callerEmail = role == CommunityRole.Resident
            ? (await CreateResidentAsync(await CreatePropertyAsync(community))).Email
            : (await CreateStaffAsync(community, role)).Email;
        using var caller = await ClientForAsync(callerEmail);

        var create = await caller.PostAsJsonAsync(Url(community), new { role = (int)CommunityRole.CommunityManager, userId = targetId });
        var end = await caller.PatchAsJsonAsync($"{Url(community)}/{managerRow}", new { end = true });

        foreach (var res in new[] { create, end })
        {
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal("FORBIDDEN", (await JsonAsync(res)).GetProperty("code").GetString());
        }
        Assert.Equal(1, await WithDbAsync(db => db.CommunityMemberships.CountAsync(m => m.CommunityId == community
            && m.Role == CommunityRole.CommunityManager && m.Status == MembershipStatus.Active)));
    }

    // FR-018: a direct appointment is only for people already in the community, and the
    // message doesn't say whether an id exists.
    [Fact]
    public async Task DirectAppointment_OfSomeoneOutsideTheCommunity_Is422_ForARealOrARandomId()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        var outsider = await WithDbAsync(CreateUserAsync);
        using var manager = await ClientForAsync(managerEmail);

        var real = await manager.PostAsJsonAsync(Url(community), new { role = (int)CommunityRole.BoardMember, userId = outsider });
        var random = await manager.PostAsJsonAsync(Url(community), new { role = (int)CommunityRole.BoardMember, userId = Guid.NewGuid().ToString() });

        foreach (var res in new[] { real, random })
        {
            Assert.Equal((HttpStatusCode)422, res.StatusCode);
            var body = await JsonAsync(res);
            Assert.Equal("Choose someone from this community, or invite them by email.", body.GetProperty("message").GetString());
            Assert.Equal("userId", Assert.Single(body.GetProperty("errors").EnumerateArray()).GetProperty("field").GetString());
        }
        Assert.Equal(await random.Content.ReadAsStringAsync(), await real.Content.ReadAsStringAsync());
    }

    // FR-019, R7: a former manager is reactivated on their old row.
    [Fact]
    public async Task ReappointingAFormerManager_ReactivatesTheirRow()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        var (formerId, _) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        var oldRow = await WithDbAsync(db => AddMembershipAsync(db, formerId, community, CommunityRole.CommunityManager,
            MembershipStatus.Inactive, DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)), DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1))));
        using var manager = await ClientForAsync(managerEmail);

        var res = await manager.PostAsJsonAsync(Url(community), new { role = (int)CommunityRole.CommunityManager, userId = formerId });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal(oldRow, (await JsonAsync(res)).GetProperty("id").GetGuid());
        var row = await WithDbAsync(db => db.CommunityMemberships.AsNoTracking().SingleAsync(m => m.Id == oldRow));
        Assert.Equal((MembershipStatus.Active, Today(), (DateOnly?)null), (row.Status, row.StartDate, row.EndDate));
        Assert.Equal(1, await WithDbAsync(db => db.CommunityMemberships.CountAsync(m => m.UserId == formerId
                                                                                   && m.Role == CommunityRole.CommunityManager)));
    }

    [Fact]
    public async Task AppointingSomeoneWhoHoldsTheRole_IsAlreadyMember_AndADuplicateInvitation_IsInvitationPending()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        var (leeId, leeEmail) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        using var manager = await ClientForAsync(managerEmail);

        var direct = await manager.PostAsJsonAsync(Url(community), new { role = (int)CommunityRole.BoardMember, userId = leeId });
        var byEmail = await manager.PostAsJsonAsync(Url(community), new { role = (int)CommunityRole.BoardMember, email = leeEmail });
        Assert.Equal((HttpStatusCode.Conflict, HttpStatusCode.Conflict), (direct.StatusCode, byEmail.StatusCode));
        Assert.Equal(("ALREADY_MEMBER", "ALREADY_MEMBER"),
            ((await JsonAsync(direct)).GetProperty("code").GetString(), (await JsonAsync(byEmail)).GetProperty("code").GetString()));

        var invited = NewEmail("dup");
        var first = await JsonAsync(await manager.PostAsJsonAsync(Url(community), new { role = (int)CommunityRole.Accountant, email = invited }));
        var again = await manager.PostAsJsonAsync(Url(community), new { role = (int)CommunityRole.Accountant, email = invited });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var body = await JsonAsync(again);
        Assert.Equal("INVITATION_PENDING", body.GetProperty("code").GetString());
        Assert.Equal(first.GetProperty("invitationId").GetGuid(), body.GetProperty("invitationId").GetGuid());
    }

    // US2-S11: the replacement is a different person, and both people are named.
    [Theory]
    [InlineData("same")]
    [InlineData("blankReplacement")]
    [InlineData("noCurrent")]
    public async Task Replace_WithTheSamePersonOrAMissingId_Is422_OnReplacementUserId(string shape)
    {
        var (_, community, patId, _, adminEmail) = await ReplaceSetupAsync();
        using var admin = await ClientForAsync(adminEmail);
        object request = shape switch
        {
            "same" => new { currentManagerUserId = (string?)patId, replacementUserId = patId },
            "blankReplacement" => new { currentManagerUserId = (string?)patId, replacementUserId = "  " },
            _ => new { currentManagerUserId = (string?)null, replacementUserId = patId },
        };

        var res = await admin.PostAsJsonAsync(ReplaceUrl(community), request);

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("code").GetString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal(("replacementUserId", "Choose a different person from this community as the new manager."),
            (error.GetProperty("field").GetString(), error.GetProperty("message").GetString()));
        var pat = await WithDbAsync(db => db.CommunityMemberships.AsNoTracking()
            .SingleAsync(m => m.CommunityId == community && m.UserId == patId));
        Assert.Equal((MembershipStatus.Active, (DateOnly?)null), (pat.Status, pat.EndDate));
        Assert.DoesNotContain(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("BoardMembershipChange"));
    }

    // US2-S17: a Company Administrator can't name themself the replacement, even from the people list.
    [Fact]
    public async Task Replace_NamingTheAdministratorThemself_IsSelfGrantNotAllowed()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (patId, _) = await CreateManagerAsync(community);
        var (adminId, adminEmail) = await CreateCompanyAdminAsync(companyId);
        // In the people list, so only the self-grant rule stands in the way.
        await WithDbAsync(db => AddMembershipAsync(db, adminId, community, CommunityRole.Accountant));
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PostAsJsonAsync(ReplaceUrl(community), new { currentManagerUserId = patId, replacementUserId = adminId });

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        Assert.Equal("SELF_GRANT_NOT_ALLOWED", (await JsonAsync(res)).GetProperty("code").GetString());
        var (pat, adminRow) = await ManagerRowsAsync(community, patId, adminId);
        Assert.Equal((MembershipStatus.Active, (DateOnly?)null), (pat!.Status, pat.EndDate));
        Assert.Null(adminRow);
    }

    // US2-S11: only a current Community Manager can be replaced.
    [Theory]
    [InlineData("boardMember")]
    [InlineData("lapsedManager")]
    [InlineData("unknownId")]
    public async Task Replace_WhenTheCurrentIsntAnEffectiveManager_Is422_OnCurrentManagerUserId(string current)
    {
        var (_, community, patId, leeId, adminEmail) = await ReplaceSetupAsync();
        var currentId = current switch
        {
            "boardMember" => (await CreateStaffAsync(community, CommunityRole.BoardMember)).UserId,
            "lapsedManager" => (await CreateManagerAsync(community, endDate: DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime).AddDays(-1))).UserId,
            _ => Guid.NewGuid().ToString(),
        };
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PostAsJsonAsync(ReplaceUrl(community), new { currentManagerUserId = currentId, replacementUserId = leeId });

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("code").GetString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal(("currentManagerUserId", "That person isn't a current Community Manager of this community."),
            (error.GetProperty("field").GetString(), error.GetProperty("message").GetString()));
        var (pat, lee) = await ManagerRowsAsync(community, patId, leeId);
        Assert.Equal((MembershipStatus.Active, (DateOnly?)null), (pat!.Status, pat.EndDate));
        Assert.Null(lee);
    }

    // US2-S11: a replacement who is already an end-dated co-manager keeps their row, made open-ended.
    [Fact]
    public async Task Replace_ByAnEndDatedCoManager_MakesTheirRowOpenEnded_AndEndsTheCurrent()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (patId, _) = await CreateManagerAsync(community);
        var (leeId, _) = await CreateManagerAsync(community, endDate: DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime).AddMonths(2));
        var leeRow = await WithDbAsync(db => db.CommunityMemberships.Where(m => m.UserId == leeId).Select(m => m.Id).SingleAsync());
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);

        var res = await admin.PostAsJsonAsync(ReplaceUrl(community), new { currentManagerUserId = patId, replacementUserId = leeId });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var appointed = (await JsonAsync(res)).GetProperty("appointed");
        Assert.Equal((leeRow, leeId, "Active", JsonValueKind.Null),
            (appointed.GetProperty("id").GetGuid(), appointed.GetProperty("userId").GetString(),
             appointed.GetProperty("status").GetString(), appointed.GetProperty("endDate").ValueKind));
        var (pat, lee) = await ManagerRowsAsync(community, patId, leeId);
        Assert.Equal((MembershipStatus.Inactive, (DateOnly?)Today()), (pat!.Status, pat.EndDate));
        Assert.Equal((leeRow, MembershipStatus.Active, (DateOnly?)null), (lee!.Id, lee.Status, lee.EndDate));

        var events = LogSink.Events.Where(e => e.MessageTemplate.Text.StartsWith("BoardMembershipChange")
                                                && e.Properties["CommunityId"].ToString() == community.ToString()).ToList();
        var grant = Assert.Single(events, e => e.Properties["Kind"].ToString() == "\"replace-grant\"");
        var end = Assert.Single(events, e => e.Properties["Kind"].ToString() == "\"replace-end\"");
        Assert.Equal((leeRow.ToString(), $"\"{leeId}\""), (grant.Properties["MembershipId"].ToString(), grant.Properties["TargetUserId"].ToString()));
        Assert.Equal(grant.Properties["Cause"].ToString(), end.Properties["Cause"].ToString());
    }

    // FR-021: a term can end today at the earliest.
    [Fact]
    public async Task InvitingWithAnEndDateInThePast_Is422_OnEndDate_AndNothingIsSent()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);
        var invited = NewEmail("late");
        var today = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime);

        var past = await manager.PostAsJsonAsync(Url(community),
            new { role = (int)CommunityRole.BoardMember, email = invited, endDate = today.AddDays(-1).ToString("yyyy-MM-dd") });

        Assert.Equal((HttpStatusCode)422, past.StatusCode);
        var body = await JsonAsync(past);
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("code").GetString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal(("endDate", "The end date can't be in the past."),
            (error.GetProperty("field").GetString(), error.GetProperty("message").GetString()));
        Assert.False(await WithDbAsync(db => db.Invitations.AnyAsync(i => i.CommunityId == community)));
        Assert.DoesNotContain(Email.Sent, m => m.Target == invited);

        var endsToday = await manager.PostAsJsonAsync(Url(community),
            new { role = (int)CommunityRole.BoardMember, email = invited, endDate = today.ToString("yyyy-MM-dd") });
        Assert.Equal(HttpStatusCode.Created, endsToday.StatusCode);
    }

    // R7: a community's first open-ended manager can't be invited with an end date.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvitingAnEndDatedManager_WhenNoManagerIsOpenEnded_IsLastManager(bool withEndDatedManager)
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding, name: "Keystone Park");
        if (withEndDatedManager)
            await CreateManagerAsync(community, endDate: DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime).AddMonths(1));
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        var pat = NewEmail("pat");
        var endDate = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime).AddMonths(6).ToString("yyyy-MM-dd");

        var res = await admin.PostAsJsonAsync(Url(community), new { role = (int)CommunityRole.CommunityManager, email = pat, endDate });

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.Equal(("LAST_MANAGER", "Keystone Park has no Community Manager with no end date yet, so its first manager can't have an end date. Invite them with no end date."),
            (body.GetProperty("code").GetString(), body.GetProperty("message").GetString()));
        Assert.False(await WithDbAsync(db => db.Invitations.AnyAsync(i => i.CommunityId == community)));
        Assert.DoesNotContain(Email.Sent, m => m.Target == pat);

        // Only the end date stood in the way.
        var openEnded = await admin.PostAsJsonAsync(Url(community), new { role = (int)CommunityRole.CommunityManager, email = pat });
        Assert.Equal(HttpStatusCode.Created, openEnded.StatusCode);
    }

    // FR-018: the people list searches full names, case-insensitively, with % and _ taken literally.
    [Fact]
    public async Task ThePeopleSearch_MatchesFullNames_AndTreatsPercentAndUnderscoreLiterally()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        var percent = await NamedStaffAsync(community, "Rate", "1%Daily");
        var lookalike = await NamedStaffAsync(community, "Rate", "100 Daily");
        var underscore = (await CreateResidentAsync(await CreatePropertyAsync(community))).UserId;
        await RenameAsync(underscore, "Zed_Alpha", "Smith");
        var noUnderscore = await NamedStaffAsync(community, "ZedXAlpha", "Jones");
        var ana = await NamedStaffAsync(community, "Ana", "Lopez");
        using var manager = await ClientForAsync(managerEmail);

        async Task<List<string>> Search(string search)
        {
            var res = await manager.GetAsync($"/api/v1/communities/{community}/people?search={Uri.EscapeDataString(search)}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var body = await JsonAsync(res);
            var ids = body.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("userId").GetString()!).ToList();
            Assert.Equal(ids.Count, body.GetProperty("total").GetInt32());
            return ids;
        }

        Assert.Equal([percent], await Search("1%d"));
        Assert.Equal([underscore], await Search("ZED_"));
        Assert.Equal([ana], await Search("  ana lop "));
        Assert.Equal(new[] { percent, lookalike }.Order(), (await Search("rate")).Order());
        Assert.Equal(new[] { underscore, noUnderscore }.Order(), (await Search("alpha")).Order());
        Assert.Equal(5, (await Search("")).Count);
    }

    [Fact]
    public async Task ThePeopleSearch_OverOneHundredCharacters_Is422_OnSearch()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var manager = await ClientForAsync(managerEmail);

        var tooLong = await manager.GetAsync($"/api/v1/communities/{community}/people?search={new string('a', 101)}");
        // Surrounding spaces don't count.
        var padded = await manager.GetAsync($"/api/v1/communities/{community}/people?search={Uri.EscapeDataString($" {new string('a', 100)} ")}");

        Assert.Equal((HttpStatusCode)422, tooLong.StatusCode);
        var body = await JsonAsync(tooLong);
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("code").GetString());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal(("search", "Search can be at most 100 characters."),
            (error.GetProperty("field").GetString(), error.GetProperty("message").GetString()));
        Assert.Equal(HttpStatusCode.OK, padded.StatusCode);
    }

    // FR-018: the people list of another community is refused like a community that doesn't exist.
    [Fact]
    public async Task ThePeopleListOfAnotherCommunity_IsForbiddenLikeANonexistentCommunity()
    {
        var companyId = await CreateCompanyAsync();
        var a = await CreateCommunityInAsync(companyId);
        var b = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(a);
        await CreateStaffAsync(b, CommunityRole.BoardMember);
        using var manager = await ClientForAsync(managerEmail);

        var inB = await manager.GetAsync($"/api/v1/communities/{b}/people");
        var missing = await manager.GetAsync($"/api/v1/communities/{Guid.NewGuid()}/people");

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (inB.StatusCode, missing.StatusCode));
        Assert.Equal("FORBIDDEN", (await JsonAsync(inB)).GetProperty("code").GetString());
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await inB.Content.ReadAsStringAsync());
    }

    private async Task<string> NamedStaffAsync(Guid community, string firstName, string lastName)
    {
        var (userId, _) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        await RenameAsync(userId, firstName, lastName);
        return userId;
    }

    private Task RenameAsync(string userId, string firstName, string lastName) => WithDbAsync(db =>
        db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.FirstName, firstName).SetProperty(u => u.LastName, lastName)));

    private async Task<(Guid CompanyId, Guid Community, string PatId, string LeeId, string AdminEmail)> ReplaceSetupAsync()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (patId, _) = await CreateManagerAsync(community);
        var (leeId, _) = await CreateStaffAsync(community, CommunityRole.BoardMember);
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        return (companyId, community, patId, leeId, adminEmail);
    }

    private Task<(CommunityMembership? Pat, CommunityMembership? Lee)> ManagerRowsAsync(Guid community, string patId, string leeId) =>
        WithDbAsync(async db =>
        {
            var rows = await db.CommunityMemberships.AsNoTracking()
                .Where(m => m.CommunityId == community && m.Role == CommunityRole.CommunityManager).ToListAsync();
            return (rows.SingleOrDefault(r => r.UserId == patId), rows.SingleOrDefault(r => r.UserId == leeId));
        });

    private static string Url(Guid community) => $"/api/v1/communities/{community}/memberships";

    private static string ReplaceUrl(Guid community) => $"/api/v1/communities/{community}/managers/replace";

    private static string NewEmail(string name) => $"{name}-{Guid.NewGuid():N}@example.com";

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Fails the save that ends a Community Manager row, after it reaches the database, once armed.</summary>
    private sealed class FailEndingAManager : SaveChangesInterceptor
    {
        private volatile bool _armed;

        public bool Armed { get => _armed; set => _armed = value; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (_armed && eventData.Context!.ChangeTracker.Entries<CommunityMembership>().Any(e =>
                    e.Entity.Role == CommunityRole.CommunityManager && e.Entity.Status == MembershipStatus.Inactive))
            {
                _armed = false;
                throw new InvalidOperationException("Injected failure after the manager row was ended.");
            }
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }
    }
}
