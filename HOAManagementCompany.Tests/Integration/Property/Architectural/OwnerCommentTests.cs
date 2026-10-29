using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Features.Property.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Property.Architectural;

/// <summary>
/// 031 US7 — the homeowner comments on their own request (text and/or files), e.g. to answer a revision request; the
/// board sees the comments in their own section under the application's details.
/// </summary>
public class OwnerCommentTests(TestDatabaseFixture fixture) : ResidentArcTestBase(fixture)
{
    private static Task<HttpResponseMessage> CommentAsync(
        HttpClient http, Guid appId, string? message, params (byte[] Bytes, string Name)[] files)
    {
        var content = new MultipartFormDataContent();
        if (message is not null)
            content.Add(new StringContent(message), "message");
        foreach (var (bytes, name) in files)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(file, "files", name);
        }
        return http.PostAsync($"{Base}/{appId}/comments", content);
    }

    // 031 US7-S1: "Given my request was denied with revisions requested, When I post 'Lowered the fence to 5ft' with
    // revised-plan.pdf, Then the comment and its file appear in my comment section and not in the submitted files."
    [Fact]
    public async Task ResidentComment_WithFile_AppearsInOwnThread()
    {
        var r = await CreateResidentAsync();
        var appId = await SeedApplicationAsync(r, new AppSpec
        {
            Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Denied, Wording = ArcDenialWording.RevisionsRequested
        });

        var res = await CommentAsync(r.Http, appId, "Lowered the fence to 5ft", (ValidPdf, "revised-plan.pdf"));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var detail = await ReadAsync<ResidentArcDetailDto>(res);
        Assert.True(detail.CanComment);
        var comment = Assert.Single(detail.Comments!);
        Assert.Equal("Lowered the fence to 5ft", comment.Body);
        var file = Assert.Single(comment.Attachments);
        Assert.Equal("revised-plan.pdf", file.FileName);
        Assert.Equal(comment.Id, Assert.Single(detail.Attachments).OwnerCommentId);
        Assert.Contains(detail.Timeline, e => e.Event == "Commented");
        var key = await WithDbAsync(db => db.ArchitecturalAttachments.Where(a => a.Id == file.Id).Select(a => a.StorageKey).SingleAsync());
        Assert.True(await ObjectExistsAsync(key));
    }

    // 031 US7-S2: "Given the owner commented, When a board member or the manager opens the application, Then the
    // comments section lists the owner's name, text, time and files, and each file opens through a short-lived link."
    [Fact]
    public async Task BoardAndManager_SeeOwnerComments()
    {
        var r = await CreateResidentAsync();
        var (_, board) = await CreateBoardClientAsync(r.CommunityId);
        var (_, manager) = await CreateBoardClientAsync(r.CommunityId, CommunityRole.CommunityManager);
        var app = await SubmitNewAsync(r);
        Assert.Equal(HttpStatusCode.Created, (await CommentAsync(r.Http, app.Id, "Survey attached", (ValidPdf, "survey.pdf"))).StatusCode);

        foreach (var viewer in new[] { board, manager })
        {
            var detail = (await viewer.GetFromJsonAsync<ArcDetailDto>($"{AppsUrl(r.CommunityId)}/{app.Id}", Json))!;
            var comment = Assert.Single(detail.OwnerComments!);
            Assert.Equal("Survey attached", comment.Body);
            Assert.False(string.IsNullOrWhiteSpace(comment.AuthorName));
            var file = Assert.Single(comment.Attachments);
            Assert.Equal(comment.Id, file.OwnerCommentId);
            var link = await viewer.GetAsync($"{AppsUrl(r.CommunityId)}/{app.Id}/attachments/{file.Id}/url");
            Assert.Equal(HttpStatusCode.OK, link.StatusCode);
        }
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcOwnerCommented"));
    }

    // 031 US7-S3: "Given I write nothing and attach nothing, When I post, Then it is refused with VALIDATION_ERROR."
    [Fact]
    public async Task EmptyComment_IsRefused()
    {
        var r = await CreateResidentAsync();
        var app = await SubmitNewAsync(r);

        await AssertErrorAsync(await CommentAsync(r.Http, app.Id, "   "), HttpStatusCode.UnprocessableEntity,
            ResidentArcErrorCodes.ValidationError);
    }

    // 031 US7-S3: a comment of only files (no text) is accepted.
    [Fact]
    public async Task FilesOnlyComment_IsAccepted()
    {
        var r = await CreateResidentAsync();
        var app = await SubmitNewAsync(r);

        var res = await CommentAsync(r.Http, app.Id, null, (ValidPdf, "a.pdf"), (ValidPdf, "b.pdf"));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal(2, Assert.Single((await ReadAsync<ResidentArcDetailDto>(res)).Comments!).Attachments.Count);
    }

    // 031 US7-S4: "Given my request was withdrawn, When I post a comment, Then it is refused with APPLICATION_CLOSED."
    [Fact]
    public async Task WithdrawnRequest_RefusesComments()
    {
        var r = await CreateResidentAsync();
        var appId = await SeedApplicationAsync(r, new AppSpec { Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Withdrawn });

        Assert.False((await DetailOfAsync(r, appId)).CanComment);
        await AssertErrorAsync(await CommentAsync(r.Http, appId, "Hello"), HttpStatusCode.Conflict, ResidentArcErrorCodes.ApplicationClosed);
    }

    // 031 US7-S5: "Given another household's request, When I post a comment on it, Then it is refused with the
    // non-disclosing 403."
    [Fact]
    public async Task OtherPropertysRequest_IsForbidden()
    {
        var owner = await CreateResidentAsync();
        var stranger = await CreateResidentAsync(owner.CommunityId);
        var app = await SubmitNewAsync(owner);

        await AssertForbiddenAsync(await CommentAsync(stranger.Http, app.Id, "Not mine"));
    }

    // 031 FR-022: at most five files per comment, and the usual type checks apply.
    [Fact]
    public async Task TooManyOrInvalidFiles_AreRefused()
    {
        var r = await CreateResidentAsync();
        var app = await SubmitNewAsync(r);

        var six = Enumerable.Range(0, 6).Select(i => (ValidPdf, $"f{i}.pdf")).ToArray();
        await AssertErrorAsync(await CommentAsync(r.Http, app.Id, "Many", six), HttpStatusCode.UnprocessableEntity,
            ResidentArcErrorCodes.ValidationError);
        var bad = await CommentAsync(r.Http, app.Id, "Bad", (ValidPdf, "ok.pdf"), (ExeRenamedPdf, "evil.pdf"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
        Assert.False(await WithDbAsync(db => db.ArchitecturalOwnerComments.AnyAsync(c => c.ApplicationId == app.Id)));
    }
}
