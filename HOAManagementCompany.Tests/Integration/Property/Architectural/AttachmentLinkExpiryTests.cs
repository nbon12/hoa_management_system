using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using HOAManagementCompany.Features.Property.Architectural;
using HOAManagementCompany.Infrastructure.Storage;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Property.Architectural;

/// <summary>
/// 029 edge case "A resident opens an attachment link after it has expired (&gt;15 min) → the link no longer
/// works and a fresh short-lived link must be issued" (FR-013, SC-004). The storage can sign a link as if it
/// were issued 16 minutes ago with the 15-minute cap, so expiry is proven against MinIO without waiting.
/// </summary>
public class AttachmentLinkExpiryTests(TestDatabaseFixture fixture) : ResidentArcTestBase(fixture)
{
    private static readonly IssueExpired Expired = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        services.RemoveAll<IDocumentStorage>();
        services.AddSingleton(Expired);
        services.AddScoped<IDocumentStorage, BackdatingStorage>();
    }

    [Fact]
    public async Task ExpiredLink_StopsWorking_AndANewRequestIssuesAWorkingLink()
    {
        var r = await CreateResidentAsync();
        var submitted = await SubmitNewAsync(r, withPdf: true);
        var url = $"{Base}/{submitted.Id}/attachments/{Assert.Single(submitted.Attachments).Id}/url";
        using var browser = new HttpClient();

        Expired.On = true;
        var stale = await ReadAsync<ResidentArcAttachmentUrlDto>(await r.Http.GetAsync(url));
        Expired.On = false;
        var fresh = await ReadAsync<ResidentArcAttachmentUrlDto>(await r.Http.GetAsync(url));

        Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync(stale.Url)).StatusCode);
        var opened = await browser.GetAsync(fresh.Url);
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Equal(ValidPdf, await opened.Content.ReadAsByteArrayAsync());
    }

    public sealed class IssueExpired
    {
        public volatile bool On;
    }

    /// <summary>Real MinIO storage; while the switch is on, links are signed as already past the 15-minute cap.</summary>
    private sealed class BackdatingStorage(IAmazonS3 s3, IOptions<StorageOptions> opts, IssueExpired expired)
        : S3DocumentStorage(s3, opts), IDocumentStorage
    {
        Task<string> IDocumentStorage.GetPreSignedUrlAsync(string storageKey, CancellationToken ct)
        {
            if (!expired.On)
                return GetPreSignedUrlAsync(storageKey, ct);
            var url = s3.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = opts.Value.BucketName, Key = storageKey, Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(15).AddMinutes(-16)
            });
            return Task.FromResult(url.Replace("https://", "http://"));
        }
    }
}
