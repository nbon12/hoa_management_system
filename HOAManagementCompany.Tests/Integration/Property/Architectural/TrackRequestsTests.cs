using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Property.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Property.Architectural;

/// <summary>029 User Story 2 — Track my architectural requests (T039), spec.md US2 AS1–AS5 plus FR-008.</summary>
public class TrackRequestsTests(TestDatabaseFixture fixture) : ResidentArcTestBase(fixture)
{
    // US2 AS1: Given a resident with requests in various states, When they open "My architectural requests",
    // Then each shows with its projected status (data-model.md status projection).
    [Theory]
    [InlineData("Draft", ResidentArcStatuses.Draft)]
    [InlineData("Open", ResidentArcStatuses.Submitted)]
    [InlineData("DecisionReached", ResidentArcStatuses.Submitted)]
    [InlineData("OpenWithUnansweredInfo", ResidentArcStatuses.MoreInfoRequested)]
    [InlineData("OpenWithAnsweredInfo", ResidentArcStatuses.Submitted)]
    [InlineData("Approved", ResidentArcStatuses.Approved)]
    [InlineData("Denied", ResidentArcStatuses.Denied)]
    [InlineData("Withdrawn", ResidentArcStatuses.Withdrawn)]
    public async Task List_ShowsEachRequestWithProjectedStatus(string state, string expectedStatus)
    {
        var r = await CreateResidentAsync();
        var asker = await CreateMemberAsync(r.CommunityId, CommunityRole.BoardMember);
        Guid id;
        if (state == "Draft")
        {
            id = (await CreateDraftAsync(r)).Id;
        }
        else
        {
            id = await SeedApplicationAsync(r, state switch
            {
                "DecisionReached" => new AppSpec { Status = ArcApplicationStatus.DecisionReached, Outcome = ArcOutcome.Approved },
                "Approved" => new AppSpec { Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Approved },
                "Denied" => new AppSpec { Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Denied, Wording = ArcDenialWording.Denied },
                "Withdrawn" => new AppSpec { Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Withdrawn },
                _ => new AppSpec()
            });
            if (state == "OpenWithUnansweredInfo")
                await SeedInfoRequestAsync(id, asker.UserId, "Please attach a plat survey");
            if (state == "OpenWithAnsweredInfo")
                await SeedInfoRequestAsync(id, asker.UserId, "Please attach a plat survey", respondedAt: Clock.UtcNow);
        }

        var item = Assert.Single((await MyListAsync(r)).Items);

        Assert.Equal(id, item.Id);
        Assert.Equal(expectedStatus, item.Status);
        Assert.Equal(state == "Draft" ? "Draft" : "Application", item.Kind);
    }

    // US2 AS1 (list shape): only the latest revision of an application is listed, and limit/offset page the
    // list (default 25, clamped to 100).
    [Fact]
    public async Task List_ShowsLatestRevisionOnly_AndPages()
    {
        var r = await CreateResidentAsync();
        var v1 = await SeedApplicationAsync(r, new AppSpec
        {
            Number = 7001, Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Denied, Wording = ArcDenialWording.Denied
        });
        var v2 = await SeedApplicationAsync(r, new AppSpec { Number = 7001, Revision = 2, PreviousRevisionId = v1 });
        for (var i = 0; i < 3; i++)
            await SeedApplicationAsync(r, new AppSpec { Number = 7100 + i });

        var all = await MyListAsync(r);
        var page = await MyListAsync(r, "?limit=2&offset=1");
        var clamped = await MyListAsync(r, "?limit=500");

        Assert.Equal(4, all.Total);
        Assert.DoesNotContain(all.Items, i => i.Id == v1);
        Assert.Equal(2, Assert.Single(all.Items, i => i.Id == v2).Revision);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(1, page.Offset);
        Assert.Equal(25, all.Limit);
        Assert.Equal(100, clamped.Limit);
    }

    // US2 AS2: Given a resident viewing a request's detail page, Then it shows the submitted fields, a
    // chronological timeline of status changes, and the attachments.
    [Fact]
    public async Task Detail_ShowsSubmittedFieldsTimelineAndAttachments()
    {
        var r = await CreateResidentAsync();
        var (_, board) = await CreateBoardClientAsync(r.CommunityId);
        var submitted = await SubmitNewAsync(r, withPdf: true);
        Clock.UtcNow = Clock.UtcNow.AddHours(1);
        var ask = await board.PostAsJsonAsync($"{AppsUrl(r.CommunityId)}/{submitted.Id}/info-requests",
            new { message = "Please attach a plat survey" });
        Assert.Equal(HttpStatusCode.Created, ask.StatusCode);
        var infoId = (await WithDbAsync(db => db.ArchitecturalInfoRequests.SingleAsync(i => i.ApplicationId == submitted.Id))).Id;
        Clock.UtcNow = Clock.UtcNow.AddHours(1);
        Assert.Equal(HttpStatusCode.OK, (await r.Http.PostAsJsonAsync(
            $"{Base}/{submitted.Id}/info-requests/{infoId}/reply", new { responseMessage = "Survey attached" })).StatusCode);

        var detail = await DetailOfAsync(r, submitted.Id);

        Assert.Equal("Fence", detail.ProjectType);
        Assert.Equal("Fence replacement — 6ft cedar", detail.ProjectTitle);
        Assert.Equal("Replace the rear fence with 6ft cedar on the existing line.", detail.Description);
        Assert.Equal(Start, detail.PlannedStartDate);
        Assert.Equal(Finish, detail.PlannedCompletionDate);
        Assert.Equal("Cedar & Co", detail.ContractorName);
        Assert.Equal("919-555-0100", detail.ContractorContact);
        var file = Assert.Single(detail.Attachments);
        Assert.Equal("fence-plan.pdf", file.FileName);
        Assert.Equal(ValidPdf.LongLength, file.SizeBytes);
        Assert.Equal(["Submitted", "InfoRequested", "InfoReplied"], detail.Timeline.Select(e => e.Event).ToArray());
        Assert.True(detail.Timeline.Zip(detail.Timeline.Skip(1)).All(p => p.First.At <= p.Second.At));
    }

    // US2 AS3 / FR-013: an attachment opens through a pre-signed link that expires within 15 minutes and never
    // through a durable public URL.
    [Fact]
    public async Task AttachmentUrl_ExpiresWithin15Minutes_NoDurableUrlInDetail()
    {
        var r = await CreateResidentAsync();
        var submitted = await SubmitNewAsync(r, withPdf: true);
        var attachmentId = Assert.Single(submitted.Attachments).Id;

        var res = await r.Http.GetAsync($"{Base}/{submitted.Id}/attachments/{attachmentId}/url");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("no-store", res.Headers.CacheControl?.ToString());
        var link = await ReadAsync<ResidentArcAttachmentUrlDto>(res);
        Assert.True(link.ExpiresAt - Clock.UtcNow <= TimeSpan.FromMinutes(15));
        Assert.Contains("Signature", link.Url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", await r.Http.GetStringAsync($"{Base}/{submitted.Id}"));
    }

    // US2 AS4: Given a request that was denied, Then the detail shows the Denied outcome, its owner-facing wording,
    // the manager's reason and the community's formal disapproval statement — and Revise and resubmit.
    [Fact]
    public async Task Detail_Denied_ShowsWordingReasonAndFormalStatement()
    {
        var r = await CreateResidentAsync();
        await SetArcSettingsAsync(r.CommunityId, statement: "Formal disapproval under §12.3 of the Declaration.");
        var id = await SeedApplicationAsync(r, new AppSpec
        {
            Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Denied, Wording = ArcDenialWording.RevisionsRequested
        });
        await WithDbAsync(async db =>
        {
            var app = await db.ArchitecturalApplications.SingleAsync(a => a.Id == id);
            app.OwnerReason = "Lower the fence to 5ft per Guideline 4.2";
            return await db.SaveChangesAsync();
        });

        var detail = await DetailOfAsync(r, id);

        Assert.Equal(ResidentArcStatuses.Denied, detail.Status);
        Assert.Equal("Denied", detail.Decision!.Outcome);
        Assert.Equal("RevisionsRequested", detail.Decision.Wording);
        Assert.Equal("Lower the fence to 5ft per Guideline 4.2", detail.OwnerReason);
        Assert.Equal("Formal disapproval under §12.3 of the Declaration.", detail.FormalDisapprovalStatement);
        Assert.Null(detail.ConditionsOfApproval);
        Assert.True(detail.CanRevise);
        Assert.False(detail.CanWithdraw);
    }

    // US2 AS4 (approval sibling): an approval with conditions shows the conditions, and no denial fields.
    [Fact]
    public async Task Detail_ApprovedWithConditions_ShowsConditions()
    {
        var r = await CreateResidentAsync();
        var id = await SeedApplicationAsync(r, new AppSpec { Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Approved });
        await WithDbAsync(async db =>
        {
            var app = await db.ArchitecturalApplications.SingleAsync(a => a.Id == id);
            app.ConditionsOfApproval = "Stain to match the existing color";
            return await db.SaveChangesAsync();
        });

        var detail = await DetailOfAsync(r, id);

        Assert.Equal(ResidentArcStatuses.Approved, detail.Status);
        Assert.Equal("Approved", detail.Decision!.Outcome);
        Assert.Equal("Stain to match the existing color", detail.ConditionsOfApproval);
        Assert.Null(detail.OwnerReason);
        Assert.Null(detail.FormalDisapprovalStatement);
        Assert.False(detail.CanRevise);
    }

    // US2 AS5 / FR-016 / SC-005: Given a request with board votes, voter identities and board comments, When the
    // resident views it, Then none of them is shown or returned.
    [Fact]
    public async Task ListAndDetail_NeverExposeVotesVotersOrComments()
    {
        var r = await CreateResidentAsync();
        var voter1 = await CreateMemberAsync(r.CommunityId, CommunityRole.BoardMember);
        var voter2 = await CreateMemberAsync(r.CommunityId, CommunityRole.BoardMember);
        await WithDbAsync(async db =>
        {
            foreach (var u in db.Users.Where(u => u.Id == voter1.UserId || u.Id == voter2.UserId))
            {
                u.FirstName = "Sentinelvoter";
                u.LastName = "Boardname";
            }
            return await db.SaveChangesAsync();
        });
        var id = await SeedApplicationAsync(r, new AppSpec());
        await WithDbAsync(async db =>
        {
            db.ArchitecturalVotes.Add(new Domain.Entities.ArchitecturalVote
                { ApplicationId = id, VoterUserId = voter1.UserId, Choice = ArcVoteChoice.Deny, Comment = "SECRET-COMMENT-1" });
            db.ArchitecturalVotes.Add(new Domain.Entities.ArchitecturalVote
                { ApplicationId = id, VoterUserId = voter2.UserId, Choice = ArcVoteChoice.Approve, Comment = "SECRET-COMMENT-2" });
            return await db.SaveChangesAsync();
        });

        var listJson = await r.Http.GetStringAsync(Base);
        var detailJson = await r.Http.GetStringAsync($"{Base}/{id}");

        foreach (var json in new[] { listJson, detailJson })
        {
            Assert.Contains(id.ToString(), json);
            Assert.DoesNotContain("SECRET-COMMENT", json);
            Assert.DoesNotContain("Sentinelvoter", json);
            Assert.DoesNotContain(voter1.UserId, json);
            Assert.DoesNotContain(voter2.UserId, json);
            Assert.DoesNotContain("\"votes\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"tally\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("voterName", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"choice\"", json, StringComparison.OrdinalIgnoreCase);
        }
    }

    // The decision isn't official until the manager records it (027 FR-025), so a DecisionReached request
    // reads as still under review, with no decision shown.
    [Fact]
    public async Task DecisionReached_NotYetRecorded_ShowsSubmittedWithNoDecision()
    {
        var r = await CreateResidentAsync();
        var id = await SeedApplicationAsync(r, new AppSpec
        {
            Status = ArcApplicationStatus.DecisionReached, Outcome = ArcOutcome.Denied, Wording = ArcDenialWording.Denied
        });

        var detail = await DetailOfAsync(r, id);

        Assert.Equal(ResidentArcStatuses.Submitted, detail.Status);
        Assert.Null(detail.Decision);
        Assert.Null(detail.OwnerReason);
        Assert.False(detail.CanWithdraw);
        Assert.False(detail.CanRevise);
    }

    // FR-008 / edge case: a submitted request's content can't be edited — there is no update or delete route
    // for an application, and its row is unchanged by the attempts.
    [Fact]
    public async Task SubmittedApplication_CannotBeEdited()
    {
        var r = await CreateResidentAsync();
        var submitted = await SubmitNewAsync(r, withPdf: true);
        var before = await WithDbAsync(db => db.ArchitecturalApplications.AsNoTracking().Include(a => a.Attachments)
            .SingleAsync(a => a.Id == submitted.Id));
        var change = CompleteDraft();
        change["projectTitle"] = "Something else entirely";

        var put = await r.Http.PutAsJsonAsync($"{Base}/{submitted.Id}", change);
        var patch = await r.Http.PatchAsJsonAsync($"{Base}/{submitted.Id}", change);
        var delete = await r.Http.DeleteAsync($"{Base}/{submitted.Id}");

        Assert.All(new[] { put, patch, delete }, res =>
            Assert.Contains(res.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed }));
        var after = await WithDbAsync(db => db.ArchitecturalApplications.AsNoTracking().Include(a => a.Attachments)
            .SingleAsync(a => a.Id == submitted.Id));
        Assert.Equal(before.ProjectTitle, after.ProjectTitle);
        Assert.Equal(before.Description, after.Description);
        Assert.Equal(before.PlannedStartDate, after.PlannedStartDate);
        Assert.Equal(before.PlannedCompletionDate, after.PlannedCompletionDate);
        Assert.Equal(before.ContractorName, after.ContractorName);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.Attachments.Select(a => a.Id).OrderBy(x => x), after.Attachments.Select(a => a.Id).OrderBy(x => x));
    }
}
