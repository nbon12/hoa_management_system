using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Features.Property.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Property.Architectural;

/// <summary>
/// 029 User Story 1 — File an architectural request (T025). One test per acceptance scenario
/// (spec.md US1 AS1–AS8), plus the concurrent-numbering edge case (SC-002) and the Independent Test.
/// </summary>
public class FileRequestTests(TestDatabaseFixture fixture) : ResidentArcTestBase(fixture)
{
    // US1 AS1: Given a resident who owns a property, When they create a Fence request with title,
    // description, planned dates and the acknowledgement and submit it, Then it is Submitted (Open) with a
    // community-unique ARC-<number>, received date = submission date, due date = received + review period.
    [Fact]
    public async Task Submit_CompleteDraft_CreatesOpenApplicationWithNumberDatesAndSnapshots()
    {
        var r = await CreateResidentAsync();
        await SetArcSettingsAsync(r.CommunityId, nextNumber: 1042, reviewDays: 45, timeZone: "America/Chicago",
            rule: ArcDecisionRule.MajorityOfVotesCastWithQuorum, lapse: ArcLapseRule.DeemedApproved);
        var draft = await CreateDraftAsync(r);

        var res = await SubmitAsync(r, draft.Id);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var detail = await ReadAsync<ResidentArcDetailDto>(res);
        Assert.Equal("ARC-1042", detail.DisplayId);
        Assert.Equal(ResidentArcStatuses.Submitted, detail.Status);

        var row = await WithDbAsync(db => db.ArchitecturalApplications.SingleAsync(a => a.Id == detail.Id));
        var today = TodayIn("America/Chicago");
        Assert.Equal(ArcApplicationStatus.Open, row.Status);
        Assert.Equal(1042, row.ApplicationNumber);
        Assert.Equal(ArcProjectType.Fence, row.ProjectType);
        Assert.Equal(today, row.ReceivedDate);
        Assert.Equal(today.AddDays(45), row.DueDate);
        Assert.Equal(ArcDecisionRule.MajorityOfVotesCastWithQuorum, row.DecisionRule);
        Assert.Equal(ArcLapseRule.DeemedApproved, row.LapseRule);
        Assert.Equal("America/Chicago", row.TimeZoneId);
        Assert.Equal(r.UserId, row.SubmittedByUserId);
        Assert.Equal("Praneeth Pattyam", row.OwnerName);
        Assert.Equal(Start, row.PlannedStartDate);
        Assert.Equal(Finish, row.PlannedCompletionDate);
        Assert.Equal("Cedar & Co", row.ContractorName);
        Assert.NotNull(row.AcknowledgedAt);
        Assert.False(await WithDbAsync(db => db.ArchitecturalApplicationDrafts.AnyAsync(d => d.Id == draft.Id)));
        Assert.Equal(1043, await WithDbAsync(db =>
            db.CommunityArcSettings.Where(s => s.CommunityId == r.CommunityId).Select(s => s.NextApplicationNumber).SingleAsync()));
    }

    // US1 AS1 (default period): with no community settings, the number starts at 1001 and the due date
    // is received + 30 days.
    [Fact]
    public async Task Submit_WithDefaultSettings_UsesARC1001AndThirtyDayPeriod()
    {
        var r = await CreateResidentAsync();

        var detail = await SubmitNewAsync(r);

        Assert.Equal("ARC-1001", detail.DisplayId);
        var today = TodayIn("America/New_York");
        Assert.Equal(today, detail.ReceivedDate);
        Assert.Equal(today.AddDays(30), detail.DueDate);
    }

    // US1 AS2: Given a resident filling out a request, When they save it without submitting, Then it is
    // stored with status Draft and does not appear in the board's Open list.
    [Fact]
    public async Task SaveDraft_IsPersistedAsDraft_AndNotInBoardOpenList()
    {
        var r = await CreateResidentAsync();
        var (_, board) = await CreateBoardClientAsync(r.CommunityId);

        var draft = await CreateDraftAsync(r);

        var row = await WithDbAsync(db => db.ArchitecturalApplicationDrafts.SingleAsync(d => d.Id == draft.Id));
        Assert.Equal("Fence replacement — 6ft cedar", row.ProjectTitle);
        Assert.Equal(r.PropertyId, row.PropertyId);
        var mine = await MyListAsync(r);
        Assert.Equal(ResidentArcStatuses.Draft, Assert.Single(mine.Items).Status);

        var boardOpen = await board.GetFromJsonAsync<ArcListResponse>($"{AppsUrl(r.CommunityId)}?status=open", Json);
        Assert.Empty(boardOpen!.Items);
        Assert.Equal(0, boardOpen.Counts.Open);
        Assert.False(await WithDbAsync(db => db.ArchitecturalApplications.AnyAsync(a => a.PropertyId == r.PropertyId)));
    }

    // US1 AS3: Given a resident with a draft, When they edit its fields and save, Then the updated values are
    // persisted and the status remains Draft.
    [Fact]
    public async Task EditDraft_PersistsUpdatedValues_AndStaysDraft()
    {
        var r = await CreateResidentAsync();
        var draft = await CreateDraftAsync(r);
        var edit = CompleteDraft();
        edit["projectType"] = "Solar";
        edit["projectTitle"] = "Roof solar array";
        edit["plannedStartDate"] = new DateOnly(2026, 12, 1);
        edit["plannedCompletionDate"] = new DateOnly(2026, 12, 15);

        var res = await r.Http.PutAsJsonAsync($"{Base}/drafts/{draft.Id}", edit);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var reread = await ReadAsync<ResidentArcDraftDto>(await r.Http.GetAsync($"{Base}/drafts/{draft.Id}"));
        Assert.Equal("Solar", reread.ProjectType);
        Assert.Equal("Roof solar array", reread.ProjectTitle);
        Assert.Equal(new DateOnly(2026, 12, 1), reread.PlannedStartDate);
        Assert.Equal(new DateOnly(2026, 12, 15), reread.PlannedCompletionDate);
        Assert.Equal(ResidentArcStatuses.Draft, Assert.Single((await MyListAsync(r)).Items).Status);
        Assert.False(await WithDbAsync(db => db.ArchitecturalApplications.AnyAsync(a => a.PropertyId == r.PropertyId)));
    }

    // US1 AS4 / FR-014 / edge case: Given a resident with a draft, When they delete it, Then the request and
    // any attachments it held are removed (objects included) and it no longer appears in their list.
    [Fact]
    public async Task DeleteDraft_RemovesDraftAndItsObjects()
    {
        var r = await CreateResidentAsync();
        var draft = await CreateDraftAsync(r);
        Assert.Equal(HttpStatusCode.Created,
            (await UploadAsync(r.Http, $"{Base}/drafts/{draft.Id}/attachments", ValidPdf, "plan.pdf")).StatusCode);
        var key = await WithDbAsync(db => db.ArchitecturalDraftAttachments.Where(a => a.DraftId == draft.Id)
            .Select(a => a.StorageKey).SingleAsync());
        Assert.True(await ObjectExistsAsync(key));

        var res = await r.Http.DeleteAsync($"{Base}/drafts/{draft.Id}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.False(await WithDbAsync(db => db.ArchitecturalApplicationDrafts.AnyAsync(d => d.Id == draft.Id)));
        Assert.False(await WithDbAsync(db => db.ArchitecturalDraftAttachments.AnyAsync(a => a.DraftId == draft.Id)));
        Assert.False(await ObjectExistsAsync(key));
        Assert.Empty((await MyListAsync(r)).Items);
    }

    // US1 AS5 / FR-003: Given a resident filling out a request, When they submit without the acknowledgement,
    // Then submission is refused and no ID is assigned.
    [Fact]
    public async Task Submit_WithoutAcknowledgement_Refused_NoNumberAllocated()
    {
        var r = await CreateResidentAsync();
        await SetArcSettingsAsync(r.CommunityId, nextNumber: 2000);
        var draft = await CreateDraftAsync(r, CompleteDraft(acknowledged: false));

        var res = await SubmitAsync(r, draft.Id);

        await AssertErrorAsync(res, HttpStatusCode.UnprocessableEntity, ResidentArcErrorCodes.AcknowledgementRequired);
        Assert.False(await WithDbAsync(db => db.ArchitecturalApplications.AnyAsync(a => a.PropertyId == r.PropertyId)));
        Assert.Equal(2000, await WithDbAsync(db =>
            db.CommunityArcSettings.Where(s => s.CommunityId == r.CommunityId).Select(s => s.NextApplicationNumber).SingleAsync()));
        Assert.True(await WithDbAsync(db => db.ArchitecturalApplicationDrafts.AnyAsync(d => d.Id == draft.Id)));
    }

    // US1 AS6 / FR-004: Given a resident filling out a request, When they enter a planned completion date
    // earlier than the planned start date, Then submission is refused with a validation error. The API refuses
    // to save those dates at all (create or edit), so a request carrying them can never be submitted.
    [Fact]
    public async Task Submit_CompletionBeforeStart_Refused()
    {
        var r = await CreateResidentAsync();
        var bad = CompleteDraft();
        bad["plannedStartDate"] = Finish;
        bad["plannedCompletionDate"] = Start;
        var draft = await CreateDraftAsync(r);

        var create = await r.Http.PostAsJsonAsync($"{Base}/drafts", bad);
        var edit = await r.Http.PutAsJsonAsync($"{Base}/drafts/{draft.Id}", bad);
        var submit = await SubmitAsync(r, draft.Id);

        await AssertErrorAsync(create, HttpStatusCode.UnprocessableEntity, ResidentArcErrorCodes.ValidationError);
        await AssertErrorAsync(edit, HttpStatusCode.UnprocessableEntity, ResidentArcErrorCodes.ValidationError);
        // The refused edit left the draft's valid dates in place, so this submit is of the original request.
        var submitted = await ReadAsync<ResidentArcDetailDto>(submit);
        Assert.Equal(Start, submitted.PlannedStartDate);
        Assert.Equal(Finish, submitted.PlannedCompletionDate);
        Assert.False(await WithDbAsync(db => db.ArchitecturalApplications.AnyAsync(a =>
            a.PropertyId == r.PropertyId && a.PlannedCompletionDate < a.PlannedStartDate)));
    }

    // US1 AS7 / FR-025: Given a request was just submitted, Then the submitting resident is sent a plain-template
    // confirmation identifying it by ARC-<number>, queued through the transactional outbox.
    [Fact]
    public async Task Submit_EnqueuesConfirmationEmailToSubmitter()
    {
        var r = await CreateResidentAsync();
        await SetArcSettingsAsync(r.CommunityId, nextNumber: 3001);

        var detail = await SubmitNewAsync(r);

        var rows = await WithDbAsync(db => db.OutboxMessages
            .Where(m => m.DedupKey == ResidentArcSubmitService.SubmittedDedupKey(detail.Id)).ToListAsync());
        var row = Assert.Single(rows);
        Assert.Equal(ArcEmailKinds.OwnerSubmitted, row.Kind);
        Assert.Equal(r.UserId, row.RecipientUserId);
        Assert.Null(row.OwnerId);
        var payload = JsonSerializer.Deserialize<JsonElement>(row.PayloadJson);
        Assert.Equal(r.Email, payload.GetProperty("Target").GetString());
        Assert.Equal("We received your architectural request ARC-3001", payload.GetProperty("Subject").GetString());
        var body = payload.GetProperty("Body").GetString()!;
        Assert.Contains("ARC-3001", body);
        Assert.Contains("Work may not begin until this request is approved.", body);
        Assert.DoesNotContain("vote", body, StringComparison.OrdinalIgnoreCase);
    }

    // US1 AS8 / FR-009: Given a request was just submitted, When the board's Open list (027) is queried, Then
    // it is present. SC-007: it is also in the resident's own list right away (well under 5 seconds).
    [Fact]
    public async Task Submit_AppearsInBoardOpenList()
    {
        var r = await CreateResidentAsync();
        var (_, board) = await CreateBoardClientAsync(r.CommunityId);
        var draft = await CreateDraftAsync(r);

        var stopwatch = Stopwatch.StartNew();
        var submitted = await ReadAsync<ResidentArcDetailDto>(await SubmitAsync(r, draft.Id));
        var mine = await MyListAsync(r);
        stopwatch.Stop();

        var item = Assert.Single(mine.Items);
        Assert.Equal(submitted.Id, item.Id);
        Assert.Equal(ResidentArcStatuses.Submitted, item.Status);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"took {stopwatch.Elapsed}");

        var boardOpen = await board.GetFromJsonAsync<ArcListResponse>($"{AppsUrl(r.CommunityId)}?status=open", Json);
        var boardItem = Assert.Single(boardOpen!.Items);
        Assert.Equal(submitted.Id, boardItem.Id);
        Assert.Equal(submitted.DisplayId, boardItem.DisplayId);
    }

    // SC-002 / edge case: two (here five) residents of one community submitting at the same moment each get a
    // distinct, sequential ARC-<number> with no collision.
    [Fact]
    public async Task ConcurrentSubmits_GetDistinctSequentialNumbers()
    {
        var first = await CreateResidentAsync();
        await SetArcSettingsAsync(first.CommunityId, nextNumber: 5001);
        var residents = new List<Resident> { first };
        for (var i = 0; i < 4; i++)
            residents.Add(await CreateResidentAsync(first.CommunityId));
        var drafts = new List<(Resident R, Guid Draft)>();
        foreach (var r in residents)
            drafts.Add((r, (await CreateDraftAsync(r)).Id));

        var responses = await Task.WhenAll(drafts.Select(d => SubmitAsync(d.R, d.Draft)));

        Assert.All(responses, res => Assert.Equal(HttpStatusCode.Created, res.StatusCode));
        var numbers = await WithDbAsync(db => db.ArchitecturalApplications
            .Where(a => a.CommunityId == first.CommunityId).Select(a => a.ApplicationNumber).OrderBy(n => n).ToListAsync());
        Assert.Equal(Enumerable.Range(5001, 5).ToList(), numbers);
    }

    // US1 Independent Test / SC-001: a draft with an attachment, once submitted, carries the attachment to the
    // application on the same private key, and the object still exists.
    [Fact]
    public async Task Submit_DraftWithAttachment_CarriesAttachmentToApplication()
    {
        var r = await CreateResidentAsync();
        var draft = await CreateDraftAsync(r);
        await UploadAsync(r.Http, $"{Base}/drafts/{draft.Id}/attachments", ValidPdf, "fence-plan.pdf");
        var draftKey = await WithDbAsync(db => db.ArchitecturalDraftAttachments.Where(a => a.DraftId == draft.Id)
            .Select(a => a.StorageKey).SingleAsync());

        var detail = await ReadAsync<ResidentArcDetailDto>(await SubmitAsync(r, draft.Id));

        var attachment = Assert.Single(detail.Attachments);
        Assert.Equal("fence-plan.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        var row = await WithDbAsync(db => db.ArchitecturalAttachments.SingleAsync(a => a.ApplicationId == detail.Id));
        Assert.Equal(draftKey, row.StorageKey);
        Assert.Equal(r.UserId, row.UploadedByUserId);
        Assert.True(await ObjectExistsAsync(draftKey));
    }
}
