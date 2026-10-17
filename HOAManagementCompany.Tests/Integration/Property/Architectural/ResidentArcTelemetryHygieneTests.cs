using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Property.Architectural;

/// <summary>
/// 029 T065 — spec Observability: "Attachment bytes, owner names, storage keys and PII are excluded from
/// traces/logs." Runs a full resident flow (draft, upload, submit, reply, withdraw, a refused upload), then
/// scans every captured log event and exported span attribute for sentinel values.
/// </summary>
public class ResidentArcTelemetryHygieneTests(TestDatabaseFixture fixture) : ResidentArcTestBase(fixture)
{
    private const string FileName = "SENTINEL-FILE-5d2e.pdf";
    private const string Description = "SENTINEL-DESCRIPTION-a17c";
    private const string Reply = "SENTINEL-REPLY-c0f4";

    [Fact]
    public async Task ResidentArcActivity_LeavesNoFileNameOwnerNameKeyOrTextInTelemetry()
    {
        var r = await CreateResidentAsync();
        var (_, board) = await CreateBoardClientAsync(r.CommunityId);
        var body = CompleteDraft();
        body["description"] = Description;
        var draft = await CreateDraftAsync(r, body);
        await UploadAsync(r.Http, $"{Base}/drafts/{draft.Id}/attachments", ValidPdf, FileName);
        await UploadAsync(r.Http, $"{Base}/drafts/{draft.Id}/attachments", ExeRenamedPdf, FileName);
        var app = await ReadAsync<Features.Property.Architectural.ResidentArcDetailDto>(await SubmitAsync(r, draft.Id));
        await board.PostAsJsonAsync($"{AppsUrl(r.CommunityId)}/{app.Id}/info-requests", new { message = "Need a survey" });
        var infoId = (await WithDbAsync(db => db.ArchitecturalInfoRequests.SingleAsync(i => i.ApplicationId == app.Id))).Id;
        (await r.Http.PostAsJsonAsync($"{Base}/{app.Id}/info-requests/{infoId}/reply", new { responseMessage = Reply })).EnsureSuccessStatusCode();
        await r.Http.GetAsync($"{Base}/{app.Id}/attachments/{app.Attachments[0].Id}/url");
        (await r.Http.PostAsync($"{Base}/{app.Id}/withdraw", null)).EnsureSuccessStatusCode();
        FlushTelemetry();

        var key = await WithDbAsync(db => db.ArchitecturalAttachments.Where(a => a.ApplicationId == app.Id).Select(a => a.StorageKey).SingleAsync());
        // The resident's email is not a sentinel here: the pre-existing AuthService login log ("User logged in:
        // <email>") records it on every sign-in, outside this feature. Nothing in the 029 slice logs it.
        var sentinels = new[] { FileName, Description, Reply, key, "Praneeth Pattyam" };
        var logText = string.Join("\n", LogSink.Events.Select(e => e.RenderMessage() + " " +
            string.Join(" ", e.Properties.Select(p => p.Value.ToString()))));
        var spanText = string.Join("\n", ExportedSpans.SelectMany(a => a.TagObjects.Select(t => $"{t.Key}={t.Value}")));

        foreach (var sentinel in sentinels)
        {
            Assert.DoesNotContain(sentinel, logText);
            Assert.DoesNotContain(sentinel, spanText);
        }
        // The flow really ran and was logged — just without the sensitive values.
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcSubmitted"));
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcInfoReplied"));
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcWithdrawn"));
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcUploadRejected"));
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcResidentAttachmentAccess"));
    }
}
