using System.Net.Http.Json;
using HOAManagementCompany.Infrastructure.Storage;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>
/// T080 — 027 Constitution Requirements (Observability): "Comment text, owner names and storage keys are excluded
/// from telemetry." Exercises votes, info requests, detail views and attachment links, then scans every captured log
/// event (rendered JSON) and exported span attribute for the sentinel values.
/// </summary>
public class ArcTelemetryHygieneTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    private const string Comment = "SENTINEL-COMMENT-7f3a";
    private const string OwnerName = "Zebulon Sentinel-Owner";
    private const string InfoMessage = "SENTINEL-INFO-91bc";

    [Fact]
    public async Task ArcActivity_LeavesNoCommentOwnerNameOrStorageKeyInTelemetry()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s, new AppSpec { OwnerName = OwnerName, Attachments = [("plan.pdf", 8)] });
        var attachment = await WithDbAsync(db => db.ArchitecturalAttachments.FirstAsync(x => x.ApplicationId == appId));
        using (var scope = NewScope())
            await scope.ServiceProvider.GetRequiredService<IDocumentStorage>().UploadAsync(attachment.StorageKey, new byte[8]);

        await LoginAsAsync(s.Board[0]);
        (await VoteAsync(s.CommunityId, appId, "RevisionsNeeded", Comment)).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync($"{AppUrl(s.CommunityId, appId)}/info-requests", new { message = InfoMessage })).EnsureSuccessStatusCode();
        await DetailAsync(s.CommunityId, appId);
        await ListAsync(s.CommunityId, "?search=Sentinel");
        (await Client.GetAsync($"{AppUrl(s.CommunityId, appId)}/attachments/{attachment.Id}/url")).EnsureSuccessStatusCode();
        FlushTelemetry();

        var sentinels = new[] { Comment, OwnerName, InfoMessage, attachment.StorageKey };
        var logText = string.Join("\n", LogSink.Events.Select(e => e.RenderMessage() + " " +
            string.Join(" ", e.Properties.Select(p => p.Value.ToString()))));
        var spanText = string.Join("\n", ExportedSpans.SelectMany(a => a.TagObjects.Select(t => $"{t.Key}={t.Value}")));

        foreach (var sentinel in sentinels)
        {
            Assert.DoesNotContain(sentinel, logText);
            Assert.DoesNotContain(sentinel, spanText);
        }
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcVoteCast"));
    }
}
