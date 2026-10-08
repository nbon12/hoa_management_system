using System.Net;
using HOAManagementCompany.Features.Property.Architectural;
using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Infrastructure.Storage;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Property.Architectural;

/// <summary>
/// 029 edge case: "Object storage is temporarily unavailable during upload → the request is not left
/// referencing a missing object; the upload fails cleanly and the resident can retry."
/// </summary>
public class AttachmentStorageFailureTests(TestDatabaseFixture fixture) : ResidentArcTestBase(fixture)
{
    private static readonly FaultSwitch Fault = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        services.RemoveAll<IDocumentStorage>();
        services.AddSingleton(Fault);
        services.AddScoped<IDocumentStorage, FaultyStorage>();
    }

    [Fact]
    public async Task Upload_StorageUnavailable_FailsCleanlyAndCanRetry()
    {
        var r = await CreateResidentAsync();
        var draft = await CreateDraftAsync(r);
        var url = $"{Base}/drafts/{draft.Id}/attachments";

        Fault.On = true;
        HttpResponseMessage failed;
        try
        {
            failed = await UploadAsync(r.Http, url, ValidPdf, "plan.pdf");
        }
        finally
        {
            Fault.On = false;
        }

        await AssertErrorAsync(failed, HttpStatusCode.ServiceUnavailable, ResidentArcErrorCodes.StorageUnavailable);
        Assert.False(await WithDbAsync(db => db.ArchitecturalDraftAttachments.AnyAsync(a => a.DraftId == draft.Id)));

        var retried = await UploadAsync(r.Http, url, ValidPdf, "plan.pdf");

        Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
        var row = await WithDbAsync(db => db.ArchitecturalDraftAttachments.SingleAsync(a => a.DraftId == draft.Id));
        Assert.True(await ObjectExistsAsync(row.StorageKey));
    }

    public sealed class FaultSwitch
    {
        public volatile bool On;
    }

    /// <summary>The real MinIO-backed storage, except uploads throw while the switch is on.</summary>
    private sealed class FaultyStorage(Amazon.S3.IAmazonS3 s3, IOptions<StorageOptions> opts, FaultSwitch fault)
        : S3DocumentStorage(s3, opts), IDocumentStorage
    {
        Task IDocumentStorage.UploadAsync(string storageKey, byte[] content, string contentType, CancellationToken ct) =>
            fault.On
                ? throw new Amazon.S3.AmazonS3Exception("simulated outage")
                : UploadAsync(storageKey, content, contentType, ct);
    }
}
