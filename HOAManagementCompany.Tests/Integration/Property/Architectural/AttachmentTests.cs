using System.Net;
using HOAManagementCompany.Features.Property.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Property.Architectural;

/// <summary>
/// 029 User Story 5 — attachments are validated and private (T035). Limits are lowered through
/// configuration (environment-level, FR-011) so every boundary is cheap to reach.
/// </summary>
public class AttachmentTests(TestDatabaseFixture fixture) : ResidentArcTestBase(fixture)
{
    private const int MaxFile = 4096;
    private const int MaxFiles = 3;
    private const int MaxTotal = 7000;

    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() => new Dictionary<string, string?>
    {
        ["Architectural:Uploads:MaxFileBytes"] = MaxFile.ToString(),
        ["Architectural:Uploads:MaxFilesPerApplication"] = MaxFiles.ToString(),
        ["Architectural:Uploads:MaxTotalBytes"] = MaxTotal.ToString(),
    };

    private static string UploadUrl(Guid draftId) => $"{Base}/drafts/{draftId}/attachments";

    public static TheoryData<string, string, string> ValidFiles => new()
    {
        { "fence-plan.pdf", "pdf", "application/pdf" },
        { "elevation.jpg", "jpeg", "image/jpeg" },
        { "site.png", "png", "image/png" },
        { "IMG_0001.heic", "heic", "image/heic" },
    };

    private static byte[] FileBytes(string kind) => kind switch
    {
        "pdf" => ValidPdf, "jpeg" => ValidJpeg, "png" => ValidPng, "heic" => ValidHeic,
        "exe" => ExeRenamedPdf, _ => TextRenamedJpg
    };

    // US5 AS1: Given a resident adding an attachment, When the file is a valid PDF, JPG, PNG or HEIC within
    // limits, Then it is accepted, stored in private object storage, and its metadata is recorded.
    [Theory]
    [MemberData(nameof(ValidFiles))]
    public async Task Upload_ValidFile_IsStoredPrivatelyWithSniffedMetadata(string fileName, string kind, string expectedType)
    {
        var r = await CreateResidentAsync();
        var draft = await CreateDraftAsync(r);
        var bytes = FileBytes(kind);

        // The client lies about the type; the server keeps the sniffed one.
        var res = await UploadAsync(r.Http, UploadUrl(draft.Id), bytes, fileName, declaredType: "application/octet-stream");

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var dto = await ReadAsync<ResidentArcAttachmentDto>(res);
        Assert.Equal(expectedType, dto.ContentType);
        Assert.Equal(fileName, dto.FileName);
        var row = await WithDbAsync(db => db.ArchitecturalDraftAttachments.SingleAsync(a => a.Id == dto.Id));
        Assert.StartsWith($"arc/{r.CommunityId}/drafts/{draft.Id}/", row.StorageKey);
        Assert.Equal(bytes.LongLength, row.SizeBytes);
        Assert.Equal(expectedType, row.ContentType);
        Assert.True(await ObjectExistsAsync(row.StorageKey));
    }

    // US5 AS2 / FR-010: Given a resident adding an attachment, When the file's actual content is not an allowed
    // type (even if its extension says it is), Then the upload is refused and the file is not stored.
    [Theory]
    [InlineData("plan.pdf", "exe", "application/pdf")]
    [InlineData("photo.jpg", "text", "image/jpeg")]
    public async Task Upload_ContentNotAllowed_RefusedAndNotStored(string fileName, string kind, string declaredType)
    {
        var r = await CreateResidentAsync();
        var draft = await CreateDraftAsync(r);

        var res = await UploadAsync(r.Http, UploadUrl(draft.Id), FileBytes(kind), fileName, declaredType);

        await AssertErrorAsync(res, HttpStatusCode.UnprocessableEntity, ResidentArcErrorCodes.UnsupportedFileType);
        Assert.False(await WithDbAsync(db => db.ArchitecturalDraftAttachments.AnyAsync(a => a.DraftId == draft.Id)));
        Assert.False(await AnyObjectUnderDraftAsync(r, draft.Id));
    }

    // US5 AS3 / FR-011: When the file exceeds the per-file size limit, Then the upload is refused.
    [Fact]
    public async Task Upload_OverPerFileLimit_Refused()
    {
        var r = await CreateResidentAsync();
        var draft = await CreateDraftAsync(r);

        var atLimit = await UploadAsync(r.Http, UploadUrl(draft.Id), Padded(ValidPdf[..9], MaxFile), "exact.pdf");
        var overLimit = await UploadAsync(r.Http, UploadUrl(draft.Id), Padded(ValidPdf[..9], MaxFile + 1), "big.pdf");

        Assert.Equal(HttpStatusCode.Created, atLimit.StatusCode);
        await AssertErrorAsync(overLimit, HttpStatusCode.UnprocessableEntity, ResidentArcErrorCodes.FileTooLarge);
        Assert.Equal(1, await WithDbAsync(db => db.ArchitecturalDraftAttachments.CountAsync(a => a.DraftId == draft.Id)));
    }

    // US5 AS4 (count): Given a request already at the per-application attachment count, When the resident adds
    // another, Then the upload is refused.
    [Fact]
    public async Task Upload_AtCountLimit_Refused()
    {
        var r = await CreateResidentAsync();
        var draft = await CreateDraftAsync(r);
        for (var i = 0; i < MaxFiles; i++)
            Assert.Equal(HttpStatusCode.Created,
                (await UploadAsync(r.Http, UploadUrl(draft.Id), Padded(ValidPdf[..9], 1024), $"p{i}.pdf")).StatusCode);

        var res = await UploadAsync(r.Http, UploadUrl(draft.Id), Padded(ValidPdf[..9], 1024), "one-too-many.pdf");

        await AssertErrorAsync(res, HttpStatusCode.UnprocessableEntity, ResidentArcErrorCodes.AttachmentLimitReached);
        Assert.Equal(MaxFiles, await WithDbAsync(db => db.ArchitecturalDraftAttachments.CountAsync(a => a.DraftId == draft.Id)));
    }

    // US5 AS4 (total): Given a request at the per-application total-size limit, When the resident adds another
    // file that would exceed it, Then the upload is refused.
    [Fact]
    public async Task Upload_OverTotalBytes_Refused()
    {
        var r = await CreateResidentAsync();
        var draft = await CreateDraftAsync(r);
        await UploadAsync(r.Http, UploadUrl(draft.Id), Padded(ValidPdf[..9], 3000), "a.pdf");
        await UploadAsync(r.Http, UploadUrl(draft.Id), Padded(ValidPdf[..9], 3000), "b.pdf");

        var res = await UploadAsync(r.Http, UploadUrl(draft.Id), Padded(ValidPdf[..9], 1500), "c.pdf"); // 7500 > 7000

        await AssertErrorAsync(res, HttpStatusCode.UnprocessableEntity, ResidentArcErrorCodes.AttachmentLimitReached);
        Assert.Equal(2, await WithDbAsync(db => db.ArchitecturalDraftAttachments.CountAsync(a => a.DraftId == draft.Id)));
    }

    // FR-013 / SC-004 / edge case "expired link": files are reached only through a link that expires within
    // 15 minutes; no list or draft response embeds a URL.
    [Fact]
    public async Task AttachmentUrl_IsShortLived_AndNeverEmbedded()
    {
        var r = await CreateResidentAsync();
        var draft = await CreateDraftAsync(r);
        var file = await ReadAsync<ResidentArcAttachmentDto>(await UploadAsync(r.Http, UploadUrl(draft.Id), ValidPdf, "plan.pdf"));

        var link = await ReadAsync<ResidentArcAttachmentUrlDto>(
            await r.Http.GetAsync($"{Base}/drafts/{draft.Id}/attachments/{file.Id}/url"));

        Assert.StartsWith("http", link.Url);
        // A signed, time-limited link: SigV4 (X-Amz-Expires) or SigV2 (Expires=), never a bare object URL.
        Assert.Contains("Expires", link.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Signature", link.Url, StringComparison.OrdinalIgnoreCase);
        Assert.True(link.ExpiresAt - Clock.UtcNow <= TimeSpan.FromMinutes(15));
        var draftJson = await r.Http.GetStringAsync($"{Base}/drafts/{draft.Id}");
        var listJson = await r.Http.GetStringAsync(Base);
        Assert.DoesNotContain("http", draftJson);
        Assert.DoesNotContain("http", listJson);
    }

    // FR-014: removing one upload from a draft deletes its row and its object.
    [Fact]
    public async Task DeleteDraftAttachment_RemovesRowAndObject()
    {
        var r = await CreateResidentAsync();
        var draft = await CreateDraftAsync(r);
        var file = await ReadAsync<ResidentArcAttachmentDto>(await UploadAsync(r.Http, UploadUrl(draft.Id), ValidPdf, "plan.pdf"));
        var key = await WithDbAsync(db => db.ArchitecturalDraftAttachments.Where(a => a.Id == file.Id).Select(a => a.StorageKey).SingleAsync());

        var res = await r.Http.DeleteAsync($"{Base}/drafts/{draft.Id}/attachments/{file.Id}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.False(await WithDbAsync(db => db.ArchitecturalDraftAttachments.AnyAsync(a => a.Id == file.Id)));
        Assert.False(await ObjectExistsAsync(key));
    }

    // FR-022 for uploads: a resident can't upload to another property's draft, and nothing is written.
    [Fact]
    public async Task Upload_ToAnotherPropertysDraft_Forbidden()
    {
        var owner = await CreateResidentAsync();
        var stranger = await CreateResidentAsync(owner.CommunityId);
        var draft = await CreateDraftAsync(owner);

        var res = await UploadAsync(stranger.Http, UploadUrl(draft.Id), ValidPdf, "plan.pdf");

        await AssertForbiddenAsync(res);
        Assert.False(await WithDbAsync(db => db.ArchitecturalDraftAttachments.AnyAsync(a => a.DraftId == draft.Id)));
        Assert.False(await AnyObjectUnderDraftAsync(owner, draft.Id));
    }

    // Constitution §7 / spec Security: a refused upload is a sensitive event that names the reason, not the file.
    [Fact]
    public async Task Upload_Rejected_LogsSensitiveEventWithoutFileName()
    {
        var r = await CreateResidentAsync();
        var draft = await CreateDraftAsync(r);

        await UploadAsync(r.Http, UploadUrl(draft.Id), ExeRenamedPdf, "secret-blueprints-for-lot-29.pdf");

        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcUploadRejected")
            && e.Properties["Reason"].ToString().Contains(ResidentArcErrorCodes.UnsupportedFileType));
        Assert.DoesNotContain(LogSink.Events, e => e.RenderMessage().Contains("secret-blueprints"));
    }

    private async Task<bool> AnyObjectUnderDraftAsync(Resident r, Guid draftId)
    {
        // Object keys end in a random GUID, so list the draft's whole prefix in MinIO.
        var prefix = $"arc/{r.CommunityId}/drafts/{draftId}/";
        return await ListKeysAsync(prefix) > 0;
    }

    private async Task<int> ListKeysAsync(string prefix)
    {
        var endpoint = new Uri(Fixture.MinioEndpoint);
        var minio = new MinioClient()
            .WithEndpoint($"{endpoint.Host}:{endpoint.Port}")
            .WithCredentials(Fixture.MinioAccessKey, Fixture.MinioSecretKey)
            .WithSSL(endpoint.Scheme == "https")
            .Build();
        var count = 0;
        await foreach (var _ in minio.ListObjectsEnumAsync(new ListObjectsArgs()
                           .WithBucket("hoa-documents").WithPrefix(prefix).WithRecursive(true)))
            count++;
        return count;
    }
}
