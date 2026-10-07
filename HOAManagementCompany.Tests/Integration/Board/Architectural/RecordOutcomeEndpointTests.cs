using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Infrastructure.Payments.Alerts;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>T054 (US6) — POST …/outcome and …/outcome/resend-email.</summary>
public class RecordOutcomeEndpointTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    private async Task<(ArcScenario s, BoardMember manager)> ArrangeAsync(bool ownerWithEmail = true)
    {
        var s = await CreateScenarioAsync(5, ownerWithEmail);
        var manager = await CreateMemberAsync(s.CommunityId, CommunityRole.CommunityManager);
        await LoginAsAsync(manager);
        return (s, manager);
    }

    private Task<HttpResponseMessage> RecordAsync(Guid c, Guid a, object body) =>
        Client.PostAsJsonAsync($"{AppUrl(c, a)}/outcome", body);

    private Task<OutboxMessage?> OutcomeRowAsync(Guid appId) =>
        WithDbAsync(db => db.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.DedupKey == $"arc:{appId}:outcome"));

    private static AlertMessage Payload(OutboxMessage m) => JsonSerializer.Deserialize<AlertMessage>(m.PayloadJson)!;

    // US6-S7: "Given an application with a reached decision of approve, When the community manager records the outcome,
    // Then the application moves to Closed with outcome "approved" and the closing date, and the owner is sent the
    // "application approved" email naming the application ID, property and project."
    [Fact]
    public async Task Approved_ClosesAndQueuesApprovedEmailToOwner_WithoutConditionsSection()
    {
        var (s, _) = await ArrangeAsync();
        var appId = await CreateApplicationAsync(s, new AppSpec
        {
            Number = 1042, Status = ArcApplicationStatus.DecisionReached, Outcome = ArcOutcome.Approved
        });

        var res = await RecordAsync(s.CommunityId, appId, new { });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var detail = (await res.Content.ReadFromJsonAsync<ArcDetailDto>(Json))!;
        Assert.Equal("Closed", detail.Status);
        Assert.NotNull(detail.ClosedAt);
        var row = (await OutcomeRowAsync(appId))!;
        Assert.Equal(ArcEmailKinds.OwnerApproved, row.Kind);
        var ownerId = await WithDbAsync(db => db.Owners.Where(o => o.PropertyId == s.PropertyId).Select(o => o.Id).SingleAsync());
        Assert.Equal(ownerId, row.OwnerId);
        var body = Payload(row).Body;
        Assert.Contains("ARC-1042", body);
        Assert.Contains("711 Keystone Park Dr #29", body);
        Assert.Contains("Fence replacement — 6ft cedar", body);
        Assert.DoesNotContain("Conditions of approval", body);
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcOutcomeRecorded"));
    }

    // US6-S8: "…When the manager records the outcome with the conditions "Fence must be stained to match the existing
    // color", Then the application closes with outcome "approved" and those conditions, and the owner's approved email
    // shows them in its conditions section."
    [Fact]
    public async Task Approved_WithConditions_StoresAndEmailsThem()
    {
        var (s, _) = await ArrangeAsync();
        var appId = await CreateApplicationAsync(s, new AppSpec { Status = ArcApplicationStatus.DecisionReached, Outcome = ArcOutcome.Approved });

        var detail = (await (await RecordAsync(s.CommunityId, appId,
            new { conditionsOfApproval = "Fence must be stained to match the existing color" })).Content.ReadFromJsonAsync<ArcDetailDto>(Json))!;

        Assert.Equal("Fence must be stained to match the existing color", detail.ConditionsOfApproval);
        var body = Payload((await OutcomeRowAsync(appId))!).Body;
        Assert.Contains("Conditions of approval:", body);
        Assert.Contains("Fence must be stained to match the existing color", body);
    }

    // US6-S10: "Given that decision and board comments, When the community manager records the outcome with the reason
    // "Lower the fence to 5ft per Guideline 4.2", Then the application closes with outcome "denied" and wording
    // "revisions requested", the board's Closed tab shows it as "Denied · revisions requested", and the owner is sent the
    // "revisions requested" email containing that reason, the community's formal disapproval statement and a
    // "Revise and resubmit" link. Board-only vote comments are not included."
    [Fact]
    public async Task RevisionsRequested_EmailHasReasonStatementAndResubmitLink_ButNoBoardComments()
    {
        var (s, _) = await ArrangeAsync();
        var appId = await CreateApplicationAsync(s, new AppSpec
        {
            Status = ArcApplicationStatus.DecisionReached, Outcome = ArcOutcome.Denied, Wording = ArcDenialWording.RevisionsRequested
        });
        await WithDbAsync(async db =>
        {
            db.ArchitecturalVotes.Add(new ArchitecturalVote
            {
                ApplicationId = appId, VoterUserId = s.Board[0].UserId, Choice = ArcVoteChoice.RevisionsNeeded,
                Comment = "BOARD-ONLY: neighbor complained"
            });
            return await db.SaveChangesAsync();
        });

        (await RecordAsync(s.CommunityId, appId, new { ownerReason = "Lower the fence to 5ft per Guideline 4.2" })).EnsureSuccessStatusCode();

        var row = (await OutcomeRowAsync(appId))!;
        Assert.Equal(ArcEmailKinds.OwnerRevisionsRequested, row.Kind);
        var body = Payload(row).Body;
        Assert.Contains("Lower the fence to 5ft per Guideline 4.2", body);
        Assert.Contains(CommunityArcSettings.DefaultFormalDisapprovalStatement, body);
        Assert.Contains($"/app/property/architectural/{appId}/revise", body);
        Assert.DoesNotContain("BOARD-ONLY", body);
        Assert.DoesNotContain("Board Tester", body);

        await LoginAsAsync(s.Board[1]);
        var closed = (await ListAsync(s.CommunityId, "?status=closed")).Items.Single(i => i.Id == appId);
        Assert.Equal(new ArcDecisionDto("Denied", "RevisionsRequested", "Votes"), closed.Decision);
    }

    // US6-S11: "Given a decision reached on the denial side with 3 deny and 1 revisions needed votes, When the manager
    // records the outcome with a reason, Then the application closes with outcome "denied" and wording "denied", and the
    // owner is sent the "denied" email with the reason, the same formal disapproval statement and the same "Revise and resubmit" link."
    [Fact]
    public async Task Denied_EmailHasReasonStatementAndResubmitLink()
    {
        var (s, _) = await ArrangeAsync();
        var appId = await CreateApplicationAsync(s, new AppSpec
        {
            Status = ArcApplicationStatus.DecisionReached, Outcome = ArcOutcome.Denied, Wording = ArcDenialWording.Denied
        });

        (await RecordAsync(s.CommunityId, appId, new { ownerReason = "Exceeds the height limit." })).EnsureSuccessStatusCode();

        var row = (await OutcomeRowAsync(appId))!;
        Assert.Equal(ArcEmailKinds.OwnerDenied, row.Kind);
        var body = Payload(row).Body;
        Assert.Contains("Exceeds the height limit.", body);
        Assert.Contains(CommunityArcSettings.DefaultFormalDisapprovalStatement, body);
        Assert.Contains($"/app/property/architectural/{appId}/revise", body);
    }

    [Fact]
    public async Task LapseDenial_ManagerMayChooseWording()
    {
        var (s, _) = await ArrangeAsync();
        var appId = await CreateApplicationAsync(s, new AppSpec
        {
            Status = ArcApplicationStatus.DecisionReached, Outcome = ArcOutcome.Denied,
            Wording = ArcDenialWording.RevisionsRequested, Source = ArcDecisionSource.Lapse
        });

        (await RecordAsync(s.CommunityId, appId, new { ownerReason = "Review period lapsed.", wording = "Denied" })).EnsureSuccessStatusCode();

        Assert.Equal(ArcEmailKinds.OwnerDenied, (await OutcomeRowAsync(appId))!.Kind);
    }

    public static TheoryData<ArcOutcome, ArcDecisionSource, string> InvalidBodies => new()
    {
        { ArcOutcome.Denied, ArcDecisionSource.Votes, """{}""" },                                         // reason required
        { ArcOutcome.Denied, ArcDecisionSource.Votes, """{"ownerReason":"x","conditionsOfApproval":"y"}""" }, // no conditions on denial
        { ArcOutcome.Denied, ArcDecisionSource.Votes, """{"ownerReason":"x","wording":"Denied"}""" },     // wording follows votes
        { ArcOutcome.Approved, ArcDecisionSource.Votes, """{"ownerReason":"x"}""" },                      // reason only for denials
    };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task InvalidOutcomeBodies_Return422_AndLeaveTheApplicationOpenForRecording(
        ArcOutcome outcome, ArcDecisionSource source, string json)
    {
        var (s, _) = await ArrangeAsync();
        var appId = await CreateApplicationAsync(s, new AppSpec
        {
            Status = ArcApplicationStatus.DecisionReached, Outcome = outcome, Source = source,
            Wording = outcome == ArcOutcome.Denied ? ArcDenialWording.Denied : null
        });

        var res = await Client.PostAsync($"{AppUrl(s.CommunityId, appId)}/outcome",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        var app = await WithDbAsync(db => db.ArchitecturalApplications.AsNoTracking().SingleAsync(a => a.Id == appId));
        Assert.Equal(ArcApplicationStatus.DecisionReached, app.Status);
        Assert.Null(await OutcomeRowAsync(appId));
    }

    [Fact]
    public async Task OpenApplication_Returns409NoDecision()
    {
        var (s, _) = await ArrangeAsync();
        var appId = await CreateApplicationAsync(s);
        var res = await RecordAsync(s.CommunityId, appId, new { });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal(ArcErrorCodes.NoDecision, await ErrorCodeAsync(res));
    }

    [Theory]
    [InlineData(CommunityRole.BoardMember)]
    [InlineData(CommunityRole.Accountant)]
    public async Task NonManagers_CannotRecordOutcomes(CommunityRole role)
    {
        var (s, _) = await ArrangeAsync();
        var appId = await CreateApplicationAsync(s, new AppSpec { Status = ArcApplicationStatus.DecisionReached, Outcome = ArcOutcome.Approved });
        await LoginAsAsync(await CreateMemberAsync(s.CommunityId, role));

        Assert.Equal(HttpStatusCode.Forbidden, (await RecordAsync(s.CommunityId, appId, new { })).StatusCode);
    }

    // Edge case "Property has no owner email": recorded and closed, no email queued, manager told NoOwnerEmail.
    [Fact]
    public async Task NoOwnerEmail_StillCloses_QueuesNothing_AndReportsNoOwnerEmail()
    {
        var (s, _) = await ArrangeAsync(ownerWithEmail: false);
        var appId = await CreateApplicationAsync(s, new AppSpec { Status = ArcApplicationStatus.DecisionReached, Outcome = ArcOutcome.Approved });

        var detail = (await (await RecordAsync(s.CommunityId, appId, new { })).Content.ReadFromJsonAsync<ArcDetailDto>(Json))!;

        Assert.Equal("Closed", detail.Status);
        Assert.Equal(ArcOwnerEmailStatuses.NoOwnerEmail, detail.OwnerEmailStatus);
        Assert.Null(await OutcomeRowAsync(appId));
    }

    // FR-026 resend: in tests no email provider is configured, so the first send fails terminally.
    [Fact]
    public async Task FailedOwnerEmail_CanBeResent_OnlyWhileFailed()
    {
        var (s, _) = await ArrangeAsync();
        var appId = await CreateApplicationAsync(s, new AppSpec { Status = ArcApplicationStatus.DecisionReached, Outcome = ArcOutcome.Approved });
        var detail = (await (await RecordAsync(s.CommunityId, appId, new { })).Content.ReadFromJsonAsync<ArcDetailDto>(Json))!;
        Assert.Equal("Failed", detail.OwnerEmailStatus);

        var resend = await Client.PostAsJsonAsync($"{AppUrl(s.CommunityId, appId)}/outcome/resend-email", new { });

        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        Assert.True(await WithDbAsync(db => db.OutboxMessages.AnyAsync(m => m.DedupKey == $"arc:{appId}:outcome:resend:1")));

        await WithDbAsync(async db =>
        {
            foreach (var m in db.OutboxMessages.Where(m => m.DedupKey!.StartsWith($"arc:{appId}:outcome")))
                m.Status = OutboxStatus.Sent;
            return await db.SaveChangesAsync();
        });
        var again = await Client.PostAsJsonAsync($"{AppUrl(s.CommunityId, appId)}/outcome/resend-email", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(ArcErrorCodes.EmailNotFailed, await ErrorCodeAsync(again));
    }
}
