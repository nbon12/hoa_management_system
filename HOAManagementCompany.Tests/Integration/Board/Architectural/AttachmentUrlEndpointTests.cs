using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Amazon.S3;
using Amazon.S3.Model;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Infrastructure.Storage;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>T039 (US3) — GET …/attachments/{attachmentId}/url against Testcontainers MinIO.</summary>
public class AttachmentUrlEndpointTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    private async Task<(ArcScenario s, Guid appId, Guid attachmentId, byte[] bytes)> ArrangeAsync(int size = 64)
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s, new AppSpec { Attachments = [("fence-plan.pdf", size)] });
        var attachment = await WithDbAsync(db => db.ArchitecturalAttachments.FirstAsync(x => x.ApplicationId == appId));
        var (attachmentId, key) = (attachment.Id, attachment.StorageKey);
        var bytes = new byte[size];
        Random.Shared.NextBytes(bytes);
        using var scope = NewScope();
        await scope.ServiceProvider.GetRequiredService<IDocumentStorage>().UploadAsync(key, bytes, "application/pdf");
        return (s, appId, attachmentId, bytes);
    }

    private static string UrlFor(Guid c, Guid a, Guid f) =>
        $"/api/v1/communities/{c}/architectural-applications/{a}/attachments/{f}/url";

    // US3-S2: "When I open fence-plan.pdf, Then it opens in a new browser tab through a link that expires within
    // 15 minutes of being issued." SC-002: the link is issued and the file downloaded within 3 seconds.
    [Fact]
    public async Task Link_DownloadsTheExactFile_ExpiresWithin15Minutes_AndIsFast()
    {
        var (s, appId, attachmentId, bytes) = await ArrangeAsync(2 * 1024 * 1024);
        await LoginAsAsync(s.Board[0]);

        var stopwatch = Stopwatch.StartNew();
        var res = await Client.GetAsync(UrlFor(s.CommunityId, appId, attachmentId));
        res.EnsureSuccessStatusCode();
        var link = (await res.Content.ReadFromJsonAsync<ArcAttachmentUrlDto>(Json))!;
        using var plain = new HttpClient();
        var downloaded = await plain.GetByteArrayAsync(link.Url);
        stopwatch.Stop();

        Assert.Equal(bytes, downloaded);
        Assert.True(link.ExpiresAt <= Clock.UtcNow.AddMinutes(15));
        var query = System.Web.HttpUtility.ParseQueryString(new Uri(link.Url).Query);
        // SigV4 carries a lifetime in seconds; SigV2 an absolute Unix expiry. Either way ≤ 15 minutes.
        var lifetime = query["X-Amz-Expires"] is { } seconds
            ? TimeSpan.FromSeconds(int.Parse(seconds))
            : DateTimeOffset.FromUnixTimeSeconds(long.Parse(query["Expires"]!)) - DateTimeOffset.UtcNow;
        Assert.True(lifetime <= TimeSpan.FromMinutes(15), $"link lifetime {lifetime}");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"took {stopwatch.Elapsed}");
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcSensitiveAccess")
            && e.Properties["Resource"].ToString().Contains($"attachment:{attachmentId}"));
    }

    // US3-S4: "Given I am not a board member of the application's community, When I request an attachment link
    // for it, Then the request is refused and no link is issued."
    [Fact]
    public async Task NonMember_AndForeignAttachmentId_AreRefusedWithoutALink()
    {
        var (s, appId, attachmentId, _) = await ArrangeAsync();
        var outsider = await CreateScenarioAsync(1);
        await LoginAsAsync(outsider.Board[0]);

        var res = await Client.GetAsync(UrlFor(s.CommunityId, appId, attachmentId));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.DoesNotContain("http", await res.Content.ReadAsStringAsync());

        var otherApp = await CreateApplicationAsync(s);
        await LoginAsAsync(s.Board[0]);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync(UrlFor(s.CommunityId, otherApp, attachmentId))).StatusCode);
    }

    // Edge case "Attachment missing from storage": 404 ATTACHMENT_UNAVAILABLE.
    [Fact]
    public async Task MissingObject_Returns404AttachmentUnavailable()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s, new AppSpec { Attachments = [("lost.pdf", 10)] });
        var attachmentId = await WithDbAsync(db => db.ArchitecturalAttachments.Where(x => x.ApplicationId == appId).Select(x => x.Id).FirstAsync());
        await LoginAsAsync(s.Board[0]);

        var res = await Client.GetAsync(UrlFor(s.CommunityId, appId, attachmentId));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(ArcErrorCodes.AttachmentUnavailable, await ErrorCodeAsync(res));
    }
}

/// <summary>
/// US3-S3: "Given an attachment link was issued more than 15 minutes ago, When anyone requests it, Then the
/// request fails." Uses a test-only storage that signs links as if they were issued 16 minutes ago with
/// the production 15-minute lifetime, so the link is already expired when requested (no wall-clock wait).
/// </summary>
public class AttachmentLinkExpiryTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    private sealed class ShortLivedStorage : S3DocumentStorage, IDocumentStorage
    {
        private readonly IAmazonS3 _s3;
        private readonly string _bucket;

        public ShortLivedStorage(IAmazonS3 s3, IOptions<StorageOptions> opts) : base(s3, opts)
        {
            _s3 = s3;
            _bucket = opts.Value.BucketName;
        }

        Task<string> IDocumentStorage.GetPreSignedUrlAsync(string storageKey, CancellationToken ct)
        {
            var url = _s3.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _bucket, Key = storageKey,
                Expires = DateTime.UtcNow.AddMinutes(15).AddMinutes(-16), Verb = HttpVerb.GET
            });
            return Task.FromResult(url.Replace("https://", "http://"));
        }
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        services.RemoveAll<IDocumentStorage>();
        services.AddScoped<IDocumentStorage, ShortLivedStorage>();
    }

    [Fact]
    public async Task ExpiredLink_IsRefusedByStorage()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s, new AppSpec { Attachments = [("fence-plan.pdf", 8)] });
        var attachment = await WithDbAsync(db => db.ArchitecturalAttachments.FirstAsync(x => x.ApplicationId == appId));
        var (attachmentId, key) = (attachment.Id, attachment.StorageKey);
        using (var scope = NewScope())
            await scope.ServiceProvider.GetRequiredService<IDocumentStorage>().UploadAsync(key, new byte[8]);
        await LoginAsAsync(s.Board[0]);

        var link = (await (await Client.GetAsync(
            $"/api/v1/communities/{s.CommunityId}/architectural-applications/{appId}/attachments/{attachmentId}/url"))
            .Content.ReadFromJsonAsync<ArcAttachmentUrlDto>(Json))!;

        using var plain = new HttpClient();
        var res = await plain.GetAsync(link.Url);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
