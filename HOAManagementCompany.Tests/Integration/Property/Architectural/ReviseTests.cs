using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Features.Property.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Property.Architectural;

/// <summary>029 User Story 6 — Revise and resubmit after a denial (T060), spec.md US6 AS1–AS4 and FR-026.</summary>
public class ReviseTests(TestDatabaseFixture fixture) : ResidentArcTestBase(fixture)
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() => new Dictionary<string, string?>
    {
        ["Architectural:Uploads:MaxFilesPerApplication"] = "3",
    };

    /// <summary>ARC-number v1, Closed/Denied, with two attachments whose objects really exist in MinIO.</summary>
    private async Task<(Resident R, Guid V1, int Number)> ArrangeDeniedAsync()
    {
        var r = await CreateResidentAsync();
        var number = Random.Shared.Next(100_000, 999_999);
        var v1 = await SeedApplicationAsync(r, new AppSpec
        {
            Number = number, Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Denied,
            Wording = ArcDenialWording.RevisionsRequested,
            Attachments = [("fence-plan.pdf", ValidPdf.Length), ("elevation.pdf", ValidPdf.Length)]
        });
        foreach (var key in await WithDbAsync(db => db.ArchitecturalAttachments.Where(a => a.ApplicationId == v1).Select(a => a.StorageKey).ToListAsync()))
            await PutObjectAsync(key, ValidPdf);
        return (r, v1, number);
    }

    private static string ReviseUrl(Guid id) => $"{Base}/{id}/revise";

    // US6 AS1: Given a request that is Closed with outcome Denied, When the resident chooses "Revise and resubmit",
    // Then a new revision is created, pre-filled from the previous one with every field editable and its
    // attachments carried over.
    [Fact]
    public async Task Revise_Denied_CreatesPrefilledDraftWithCarriedAttachments()
    {
        var (r, v1, number) = await ArrangeDeniedAsync();

        var res = await r.Http.PostAsync(ReviseUrl(v1), null);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var draft = await ReadAsync<ResidentArcDraftDto>(res);
        Assert.Equal(v1, draft.PreviousRevisionId);
        Assert.Equal($"ARC-{number}", draft.PreviousDisplayId);
        Assert.Equal("Fence", draft.ProjectType);
        Assert.Equal("Fence replacement — 6ft cedar", draft.ProjectTitle);
        Assert.Equal("Replace the rear fence.", draft.Description);
        Assert.False(draft.Acknowledged); // a resubmission needs a fresh acknowledgement
        Assert.Equal(["elevation.pdf", "fence-plan.pdf"], draft.CarriedAttachments.Select(a => a.FileName).OrderBy(n => n).ToArray());
        Assert.Empty(draft.Attachments);
        // Every field is editable: a full edit is accepted.
        var edit = CompleteDraft();
        edit["projectTitle"] = "Fence replacement — 5ft cedar";
        Assert.Equal(HttpStatusCode.OK, (await r.Http.PutAsJsonAsync($"{Base}/drafts/{draft.Id}", edit)).StatusCode);
    }

    // US6 AS2: Given a revision pre-filled from a denial, When the resident removes a carried-over attachment,
    // Then it is removed from the revision only; the earlier revision's attachments (and objects) are unchanged.
    [Fact]
    public async Task RemoveCarriedAttachment_DoesNotTouchEarlierRevision()
    {
        var (r, v1, _) = await ArrangeDeniedAsync();
        var draft = await ReadAsync<ResidentArcDraftDto>(await r.Http.PostAsync(ReviseUrl(v1), null));
        var removed = draft.CarriedAttachments.Single(a => a.FileName == "elevation.pdf");
        var kept = draft.CarriedAttachments.Single(a => a.FileName == "fence-plan.pdf");
        var edit = CompleteDraft();
        edit["removedCarriedAttachmentIds"] = new[] { removed.Id };
        Assert.Equal(HttpStatusCode.OK, (await r.Http.PutAsJsonAsync($"{Base}/drafts/{draft.Id}", edit)).StatusCode);
        await UploadAsync(r.Http, $"{Base}/drafts/{draft.Id}/attachments", ValidPng, "new-elevation.png", "image/png");

        var v2 = await ReadAsync<ResidentArcDetailDto>(await SubmitAsync(r, draft.Id));

        var v1Rows = await WithDbAsync(db => db.ArchitecturalAttachments.Where(a => a.ApplicationId == v1).ToListAsync());
        Assert.Equal(2, v1Rows.Count);
        foreach (var row in v1Rows)
            Assert.True(await ObjectExistsAsync(row.StorageKey));
        Assert.Equal(["fence-plan.pdf", "new-elevation.png"], v2.Attachments.Select(a => a.FileName).OrderBy(n => n).ToArray());
        var keptKey = v1Rows.Single(a => a.Id == kept.Id).StorageKey;
        var v2Rows = await WithDbAsync(db => db.ArchitecturalAttachments.Where(a => a.ApplicationId == v2.Id).ToListAsync());
        Assert.Contains(v2Rows, a => a.StorageKey == keptKey); // carried on the same key, not copied
        Assert.DoesNotContain(v2Rows, a => a.StorageKey == v1Rows.Single(x => x.Id == removed.Id).StorageKey);
    }

    // US6 AS3 / FR-026: When the resident submits the revision, Then it becomes Open as ARC-<n> revision 2, with
    // its own received and due dates, no votes, linked to revision 1, and the board sees it.
    [Fact]
    public async Task SubmitRevision_CreatesOpenV2WithNewDatesAndNoVotes()
    {
        var (r, v1, number) = await ArrangeDeniedAsync();
        var (_, board) = await CreateBoardClientAsync(r.CommunityId);
        var draft = await ReadAsync<ResidentArcDraftDto>(await r.Http.PostAsync(ReviseUrl(v1), null));
        await r.Http.PutAsJsonAsync($"{Base}/drafts/{draft.Id}", CompleteDraft());

        var res = await SubmitAsync(r, draft.Id);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var v2 = await ReadAsync<ResidentArcDetailDto>(res);
        Assert.Equal($"ARC-{number}", v2.DisplayId);
        Assert.Equal(2, v2.Revision);
        Assert.Equal(ResidentArcStatuses.Submitted, v2.Status);
        var today = TodayIn("America/New_York");
        Assert.Equal(today, v2.ReceivedDate);
        Assert.Equal(today.AddDays(30), v2.DueDate);
        Assert.Equal([v1, v2.Id], v2.Revisions.Select(x => x.Id).ToArray());
        var row = await WithDbAsync(db => db.ArchitecturalApplications.Include(a => a.Votes).SingleAsync(a => a.Id == v2.Id));
        Assert.Equal(v1, row.PreviousRevisionId);
        Assert.Equal(number, row.ApplicationNumber);
        Assert.Equal(ArcApplicationStatus.Open, row.Status);
        Assert.Empty(row.Votes);
        Assert.False(await WithDbAsync(db => db.ArchitecturalApplicationDrafts.AnyAsync(d => d.Id == draft.Id)));

        var boardOpen = await board.GetFromJsonAsync<ArcListResponse>($"{AppsUrl(r.CommunityId)}?status=open", Json);
        Assert.Equal(2, Assert.Single(boardOpen!.Items, i => i.Id == v2.Id).Revision);
        var boardDetail = await board.GetFromJsonAsync<ArcDetailDto>($"{AppsUrl(r.CommunityId)}/{v2.Id}", Json);
        Assert.Equal([1, 2], boardDetail!.Revisions.Select(x => x.Revision).ToArray());
    }

    // US6 AS4: a request that is not Closed/Denied can't be revised and resubmitted; no draft is created.
    [Theory]
    [InlineData(ArcApplicationStatus.Open, null)]
    [InlineData(ArcApplicationStatus.DecisionReached, ArcOutcome.Denied)]
    [InlineData(ArcApplicationStatus.Closed, ArcOutcome.Approved)]
    [InlineData(ArcApplicationStatus.Closed, ArcOutcome.Withdrawn)]
    public async Task Revise_NotDenied_Refused(ArcApplicationStatus status, ArcOutcome? outcome)
    {
        var r = await CreateResidentAsync();
        var id = await SeedApplicationAsync(r, new AppSpec { Status = status, Outcome = outcome });

        var res = await r.Http.PostAsync(ReviseUrl(id), null);

        await AssertErrorAsync(res, HttpStatusCode.Conflict, ResidentArcErrorCodes.RevisionNotAllowed);
        Assert.False(await WithDbAsync(db => db.ArchitecturalApplicationDrafts.AnyAsync(d => d.PreviousRevisionId == id)));
    }

    // One revision in progress at a time.
    [Fact]
    public async Task Revise_Twice_RefusedWhileDraftExists()
    {
        var (r, v1, _) = await ArrangeDeniedAsync();
        Assert.Equal(HttpStatusCode.Created, (await r.Http.PostAsync(ReviseUrl(v1), null)).StatusCode);

        var again = await r.Http.PostAsync(ReviseUrl(v1), null);

        await AssertErrorAsync(again, HttpStatusCode.Conflict, ResidentArcErrorCodes.RevisionNotAllowed);
        Assert.Equal(1, await WithDbAsync(db => db.ArchitecturalApplicationDrafts.CountAsync(d => d.PreviousRevisionId == v1)));
    }

    // Only the latest revision can be revised (027 edge case "Revision submitted while an earlier one is open").
    [Fact]
    public async Task Revise_OlderRevision_Refused()
    {
        var (r, v1, number) = await ArrangeDeniedAsync();
        await SeedApplicationAsync(r, new AppSpec
        {
            Number = number, Revision = 2, PreviousRevisionId = v1, Status = ArcApplicationStatus.Closed,
            Outcome = ArcOutcome.Denied, Wording = ArcDenialWording.Denied
        });

        var res = await r.Http.PostAsync(ReviseUrl(v1), null);

        await AssertErrorAsync(res, HttpStatusCode.Conflict, ResidentArcErrorCodes.RevisionNotAllowed);
    }

    // FR-011 on a revision: carried-over files count toward the per-application limit.
    [Fact]
    public async Task Revise_CountLimitIncludesCarriedFiles()
    {
        var (r, v1, _) = await ArrangeDeniedAsync(); // 2 carried files, limit 3
        var draft = await ReadAsync<ResidentArcDraftDto>(await r.Http.PostAsync(ReviseUrl(v1), null));
        var url = $"{Base}/drafts/{draft.Id}/attachments";

        var third = await UploadAsync(r.Http, url, ValidPdf, "survey.pdf");
        var fourth = await UploadAsync(r.Http, url, ValidPdf, "extra.pdf");

        Assert.Equal(HttpStatusCode.Created, third.StatusCode);
        await AssertErrorAsync(fourth, HttpStatusCode.UnprocessableEntity, ResidentArcErrorCodes.AttachmentLimitReached);
    }
}
