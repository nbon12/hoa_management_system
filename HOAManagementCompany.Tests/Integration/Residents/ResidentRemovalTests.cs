using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Residents;

/// <summary>
/// 030 T111 — removing a resident from a home (US8; FR-024c, R9;
/// contracts/community-settings.md § DELETE …/residents/{propertyId}/{userId}).
/// </summary>
public class ResidentRemovalTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private const string Address = "711 Keystone Park Dr #29";

    // US8-S6
    [Fact]
    public async Task RemovingJane_WhoEnrolledAutopay_EndsHerAccessAndTheAutopay_AndTellsEveryone()
    {
        var (community, home, managerId, manager) = await CommunityWithHomeAsync();
        var (janeId, janeEmail) = await CreateResidentAsync(home);
        var (samId, samEmail) = await CreateResidentAsync(home);
        var otherHome = await CreatePropertyAsync(await CreateCommunityInAsync(await CreateCompanyAsync()));
        await LinkResidentAsync(janeId, otherHome);
        using var jane = ClientWithToken(MintToken(janeId, home, community));
        var schedule = await EnrollAsync(jane);
        Assert.Equal(janeId, await WithDbAsync(db => db.RecurringPayments.Where(r => r.Id == schedule).Select(r => r.EnrolledByUserId).SingleAsync()));
        Assert.Equal(HttpStatusCode.OK, (await jane.GetAsync("/api/v1/property")).StatusCode);

        var res = await manager.DeleteAsync(Url(community, home, janeId));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        Assert.True(body.GetProperty("removed").GetBoolean());
        Assert.True(body.GetProperty("autopayCancelled").GetBoolean());

        var next = await jane.GetAsync("/api/v1/property");
        Assert.Equal(HttpStatusCode.Forbidden, next.StatusCode);
        Assert.Equal("PROPERTY_ACCESS_DENIED", (await JsonAsync(next)).GetProperty("code").GetString());

        var row = await ScheduleAsync(schedule);
        Assert.Equal(("inactive", RecurringCancellationReasons.ResidentRemoved), (row.Status, row.CancellationReason));
        Assert.NotNull(row.Authorizations.Single(a => a.Id == row.CurrentAuthorizationId).TerminatedAt);
        await RunDraftsAsync(row.DraftDay);
        Assert.Empty(Stripe.OffSessionCharges);

        var toJane = Assert.Single(Email.Sent, m => m.Target == janeEmail);
        Assert.Equal($"You were removed from {Address}", toJane.Subject);
        Assert.Contains("Autopay you set up for this home has ended.", toJane.Body);
        var toSam = Assert.Single(Email.Sent, m => m.Target == samEmail);
        Assert.Equal($"Autopay for {Address} has ended", toSam.Subject);
        Assert.DoesNotContain("Jane", toSam.Body);
        Assert.Equal([CompanyEmailKinds.AutopayEnded], await KindsForAsync(samId));

        Assert.True(await WithDbAsync(db => db.UserProperties.AnyAsync(up => up.UserId == janeId && up.PropertyId == otherHome)));
        Assert.True(await WithDbAsync(db => db.Users.AnyAsync(u => u.Id == janeId)));
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ResidentLinkChange")
            && e.Properties["Change"].ToString() == "\"Removed\"" && e.Properties["ActorId"].ToString() == $"\"{managerId}\""
            && e.Properties["TargetUserId"].ToString() == $"\"{janeId}\"");
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("AutopayCancelled")
            && e.Properties["Reason"].ToString() == $"\"{RecurringCancellationReasons.ResidentRemoved}\"");
    }

    [Fact]
    public async Task WithNoRecordedEnroller_TheAutopayIsCancelledTheSameWay()
    {
        var (community, home, _, manager) = await CommunityWithHomeAsync();
        var (janeId, _) = await CreateResidentAsync(home);
        var schedule = await CreateAutopayAsync(home, enrolledBy: null);

        var body = await JsonAsync(await manager.DeleteAsync(Url(community, home, janeId)));

        Assert.True(body.GetProperty("autopayCancelled").GetBoolean());
        var row = await ScheduleAsync(schedule);
        Assert.Equal(("inactive", RecurringCancellationReasons.ResidentRemoved), (row.Status, row.CancellationReason));
        Assert.NotNull(row.Authorizations.Single(a => a.Id == row.CurrentAuthorizationId).TerminatedAt);
    }

    // US8-S7
    [Fact]
    public async Task RemovingJane_WhenSamEnrolled_LeavesTheAutopayRunning()
    {
        var (community, home, _, manager) = await CommunityWithHomeAsync();
        var (janeId, janeEmail) = await CreateResidentAsync(home);
        var (samId, _) = await CreateResidentAsync(home);
        var schedule = await CreateAutopayAsync(home, enrolledBy: samId);

        var body = await JsonAsync(await manager.DeleteAsync(Url(community, home, janeId)));

        Assert.False(body.GetProperty("autopayCancelled").GetBoolean());
        Assert.Equal("active", (await ScheduleAsync(schedule)).Status);
        Assert.Empty(await KindsForAsync(samId));
        var toJane = Assert.Single(Email.Sent, m => m.Target == janeEmail);
        Assert.DoesNotContain("Autopay", toJane.Body);
    }

    // US8-S8
    [Fact]
    public async Task RemovingJanesOnlyHome_KeepsHerBoardMembership_AndSheLandsInBoardMode()
    {
        var (community, home, _, manager) = await CommunityWithHomeAsync();
        var (janeId, janeEmail) = await CreateResidentAsync(home);
        await WithDbAsync(async db =>
        {
            db.CommunityMemberships.Add(new CommunityMembership
            {
                Id = Guid.NewGuid(), CommunityId = community, UserId = janeId, Role = CommunityRole.BoardMember,
                Status = MembershipStatus.Active, StartDate = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime).AddDays(-30),
            });
            await db.SaveChangesAsync();
        });
        var before = await MembershipsOfAsync(janeId);

        Assert.Equal(HttpStatusCode.OK, (await manager.DeleteAsync(Url(community, home, janeId))).StatusCode);

        Assert.Equal(before, await MembershipsOfAsync(janeId));
        var login = await CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = janeEmail, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var me = await JsonAsync(login);
        Assert.Equal("Board", me.GetProperty("user").GetProperty("landing").GetString());
        Assert.Empty(me.GetProperty("user").GetProperty("properties").EnumerateArray());
    }

    [Fact]
    public async Task TheRemovalPreview_SaysWhatHappensToTheAutopay()
    {
        var (community, home, _, manager) = await CommunityWithHomeAsync();
        var (janeId, _) = await CreateResidentAsync(home);
        var (samId, _) = await CreateResidentAsync(home);
        var schedule = await CreateAutopayAsync(home, enrolledBy: janeId);

        Assert.Equal(("WillCancel", "EnrolledByThisResident", 1), await PreviewAsync(manager, community, home, janeId));
        Assert.Equal(("Unaffected", "EnrolledByAnotherResident", 0), await PreviewAsync(manager, community, home, samId));

        await WithDbAsync(db => db.RecurringPayments.Where(r => r.Id == schedule)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.EnrolledByUserId, (string?)null)));
        Assert.Equal(("WillCancel", "NoEnrollerRecorded", 1), await PreviewAsync(manager, community, home, samId));

        await WithDbAsync(db => db.RecurringPayments.Where(r => r.Id == schedule)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "inactive")));
        Assert.Equal(("None", (string?)null, 0), await PreviewAsync(manager, community, home, samId));
        // The preview changes nothing.
        Assert.Equal(2, await WithDbAsync(db => db.UserProperties.CountAsync(up => up.PropertyId == home)));
    }

    [Fact]
    public async Task RemovingTheLastResident_LeavesTheHomeWithNoResident_AndTheOwnerAndBalanceAsTheyWere()
    {
        var (community, home, _, manager) = await CommunityWithHomeAsync();
        var (janeId, _) = await CreateResidentAsync(home);
        await WithDbAsync(async db =>
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(), PropertyId = home, Sequence = 1, EntryDate = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime),
                Description = "Assessment", ChargeAmount = 250m, RunningBalance = 250m, EntryType = LedgerEntryType.RegularAssessment,
            });
            await db.SaveChangesAsync();
        });
        var owner = await OwnerAndBalanceAsync(home);

        Assert.Equal(HttpStatusCode.OK, (await manager.DeleteAsync(Url(community, home, janeId))).StatusCode);

        var list = await JsonAsync(await manager.GetAsync($"/api/v1/communities/{community}/residents?filter=noResident"));
        var row = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal(home, row.GetProperty("propertyId").GetGuid());
        Assert.Empty(row.GetProperty("residentNames").EnumerateArray());
        Assert.Equal(owner, await OwnerAndBalanceAsync(home));
    }

    [Fact]
    public async Task RemovingSomeoneWhoIsntLinked_IsNotFound()
    {
        var (community, home, _, manager) = await CommunityWithHomeAsync();
        var strangerId = await WithDbAsync(CreateUserAsync);

        var res = await manager.DeleteAsync(Url(community, home, strangerId));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("NOT_FOUND", (await JsonAsync(res)).GetProperty("code").GetString());
    }

    private async Task<(Guid Community, Guid Home, string ManagerId, HttpClient Manager)> CommunityWithHomeAsync()
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync());
        var (managerId, managerEmail) = await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community, Address);
        return (community, home, managerId, await ClientForAsync(managerEmail));
    }

    private static string Url(Guid community, Guid home, string userId) =>
        $"/api/v1/communities/{community}/residents/{home}/{userId}";

    private async Task<Guid> EnrollAsync(HttpClient resident)
    {
        var setupIntent = $"seti_{Guid.NewGuid():N}";
        Stripe.SetVaultedMethod(setupIntent, new StripeVaultedMethod($"pm_{setupIntent}", "mandate_test", "card",
            CardFunding.Credit, "visa", "4242"));
        var res = await resident.PutAsJsonAsync("/api/v1/payments/recurring",
            new { amountType = "fixed", fixedAmount = 100m, draftDay = 15, setupIntentId = setupIntent, mandateAccepted = true });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await JsonAsync(res)).GetProperty("id").GetGuid();
    }

    private async Task RunDraftsAsync(int draftDay)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/payments/jobs/run-drafts?date=2026-12-{draftDay:D2}");
        request.Headers.Add("X-Scheduler-Secret", Services.GetRequiredService<IConfiguration>()["Jobs:SchedulerSharedSecret"]);
        Assert.Equal(HttpStatusCode.OK, (await CreateClient().SendAsync(request)).StatusCode);
    }

    private async Task<(string Autopay, string? Reason, int Others)> PreviewAsync(HttpClient client, Guid community, Guid home, string userId)
    {
        var res = await client.GetAsync($"/api/v1/communities/{community}/residents/{home}/removal-preview?userId={userId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await JsonAsync(res);
        return (body.GetProperty("autopay").GetString()!, body.GetProperty("autopayReason").GetString(),
            body.GetProperty("otherResidentsEmailed").GetInt32());
    }

    private Task<RecurringPayment> ScheduleAsync(Guid id) => WithDbAsync(db => db.RecurringPayments.AsNoTracking()
        .Include(r => r.Authorizations).SingleAsync(r => r.Id == id));

    private Task<List<string>> KindsForAsync(string userId) =>
        WithDbAsync(db => db.OutboxMessages.Where(m => m.RecipientUserId == userId).Select(m => m.Kind).ToListAsync());

    private Task<string> MembershipsOfAsync(string userId) => WithDbAsync(async db => string.Join(",",
        await db.CommunityMemberships.AsNoTracking().Where(m => m.UserId == userId).OrderBy(m => m.Id)
            .Select(m => $"{m.Id}:{m.Role}:{m.Status}:{m.StartDate}:{m.EndDate}").ToListAsync()));

    private Task<string> OwnerAndBalanceAsync(Guid home) => WithDbAsync(async db =>
    {
        var owner = await db.Owners.AsNoTracking().SingleAsync(o => o.PropertyId == home);
        var balance = await db.LedgerEntries.Where(e => e.PropertyId == home).OrderByDescending(e => e.Sequence)
            .Select(e => e.RunningBalance).FirstOrDefaultAsync();
        return $"{owner.Id}:{owner.FirstName}:{owner.LastName}:{owner.Email}:{balance}";
    });
}
