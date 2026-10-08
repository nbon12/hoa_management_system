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

/// <summary>029 User Story 3 — Respond to a request for more information (T046), spec.md US3 AS1–AS3.</summary>
public class InfoReplyTests(TestDatabaseFixture fixture) : ResidentArcTestBase(fixture)
{
    private const string Question = "Please attach a plat survey";

    /// <summary>A submitted request and a board member who has asked the question through 027's endpoint.</summary>
    private async Task<(Resident R, Guid AppId, Guid InfoId, HttpClient Board)> ArrangeAsync()
    {
        var r = await CreateResidentAsync();
        var (_, board) = await CreateBoardClientAsync(r.CommunityId);
        var app = await SubmitNewAsync(r);
        var ask = await board.PostAsJsonAsync($"{AppsUrl(r.CommunityId)}/{app.Id}/info-requests", new { message = Question });
        Assert.Equal(HttpStatusCode.Created, ask.StatusCode);
        var infoId = (await WithDbAsync(db => db.ArchitecturalInfoRequests.SingleAsync(i => i.ApplicationId == app.Id))).Id;
        return (r, app.Id, infoId, board);
    }

    private static string ReplyUrl(Guid appId, Guid infoId) => $"{Base}/{appId}/info-requests/{infoId}/reply";

    private async Task<JsonElement> DashboardAlertAsync(Resident r)
    {
        var dashboard = await r.Http.GetFromJsonAsync<JsonElement>("/api/v1/dashboard");
        return dashboard.GetProperty("architecturalInfoRequested");
    }

    // US3 AS1: Given a board member used "Request info" on a resident's request, When the resident opens the app,
    // Then they see the question on the request and a dashboard alert that more information is requested.
    [Fact]
    public async Task InfoRequested_ShownOnRequestAndDashboard()
    {
        var (r, appId, infoId, _) = await ArrangeAsync();

        var detail = await DetailOfAsync(r, appId);
        var alert = await DashboardAlertAsync(r);

        Assert.Equal(ResidentArcStatuses.MoreInfoRequested, detail.Status);
        var info = Assert.Single(detail.InfoRequests);
        Assert.Equal(infoId, info.Id);
        Assert.Equal(Question, info.Message);
        Assert.Null(info.RespondedAt);
        Assert.Equal(1, alert.GetProperty("count").GetInt32());
        Assert.Equal(appId, alert.GetProperty("applicationId").GetGuid());
    }

    // US3 AS2 / FR-018: Given a request flagged "more info requested", When the resident replies (with an
    // attachment), Then the reply is stored, visible to the board and the manager, and the marker is cleared.
    [Fact]
    public async Task Reply_StoresReply_ClearsMarker_VisibleToBoard()
    {
        var (r, appId, infoId, board) = await ArrangeAsync();
        var (_, manager) = await CreateBoardClientAsync(r.CommunityId, CommunityRole.CommunityManager);
        var upload = await UploadAsync(r.Http, $"{Base}/{appId}/info-requests/{infoId}/attachments", ValidPdf, "plat-survey.pdf");
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var replyFile = await ReadAsync<ResidentArcAttachmentDto>(upload);

        var res = await r.Http.PostAsJsonAsync(ReplyUrl(appId, infoId), new { responseMessage = "Survey attached" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var row = await WithDbAsync(db => db.ArchitecturalInfoRequests.SingleAsync(i => i.Id == infoId));
        Assert.NotNull(row.RespondedAt);
        Assert.Equal("Survey attached", row.ResponseMessage);
        Assert.Equal(r.UserId, row.RespondedByUserId);
        Assert.Equal(ResidentArcStatuses.Submitted, (await ReadAsync<ResidentArcDetailDto>(res)).Status);
        Assert.Equal(0, (await DashboardAlertAsync(r)).GetProperty("count").GetInt32());

        var boardList = await board.GetFromJsonAsync<ArcListResponse>($"{AppsUrl(r.CommunityId)}?status=open", Json);
        Assert.False(Assert.Single(boardList!.Items).InfoRequested);
        foreach (var viewer in new[] { board, manager })
        {
            var boardDetail = await viewer.GetFromJsonAsync<ArcDetailDto>($"{AppsUrl(r.CommunityId)}/{appId}", Json);
            var info = Assert.Single(boardDetail!.InfoRequests);
            Assert.Equal("Survey attached", info.ResponseMessage);
            Assert.NotNull(info.RespondedAt);
            var file = Assert.Single(boardDetail.Attachments, a => a.Id == replyFile.Id);
            Assert.Equal(infoId, file.InfoRequestId);
            Assert.Equal("plat-survey.pdf", file.FileName);
        }
    }

    // US3 AS3 / FR-019: while more information is requested, and after the reply, the due date doesn't move.
    [Fact]
    public async Task InfoRequest_DoesNotMoveDueDate()
    {
        var r = await CreateResidentAsync();
        var (_, board) = await CreateBoardClientAsync(r.CommunityId);
        var app = await SubmitNewAsync(r);
        var dueBefore = app.DueDate;
        Clock.UtcNow = Clock.UtcNow.AddDays(3);
        await board.PostAsJsonAsync($"{AppsUrl(r.CommunityId)}/{app.Id}/info-requests", new { message = Question });
        var infoId = (await WithDbAsync(db => db.ArchitecturalInfoRequests.SingleAsync(i => i.ApplicationId == app.Id))).Id;

        var dueWhileOutstanding = (await DetailOfAsync(r, app.Id)).DueDate;
        Clock.UtcNow = Clock.UtcNow.AddDays(5);
        await r.Http.PostAsJsonAsync(ReplyUrl(app.Id, infoId), new { responseMessage = "Survey attached" });
        var dueAfterReply = (await DetailOfAsync(r, app.Id)).DueDate;

        Assert.Equal(dueBefore, dueWhileOutstanding);
        Assert.Equal(dueBefore, dueAfterReply);
        Assert.Equal(dueBefore, await WithDbAsync(db =>
            db.ArchitecturalApplications.Where(a => a.Id == app.Id).Select(a => (DateOnly?)a.DueDate).SingleAsync()));
    }

    // A question can be answered once; a second reply is refused and the first stands.
    [Fact]
    public async Task Reply_Twice_Refused()
    {
        var (r, appId, infoId, _) = await ArrangeAsync();
        await r.Http.PostAsJsonAsync(ReplyUrl(appId, infoId), new { responseMessage = "First answer" });

        var again = await r.Http.PostAsJsonAsync(ReplyUrl(appId, infoId), new { responseMessage = "Second answer" });

        await AssertErrorAsync(again, HttpStatusCode.Conflict, ResidentArcErrorCodes.InfoAlreadyAnswered);
        Assert.Equal("First answer", await WithDbAsync(db =>
            db.ArchitecturalInfoRequests.Where(i => i.Id == infoId).Select(i => i.ResponseMessage).SingleAsync()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reply_Blank_Refused(string message)
    {
        var (r, appId, infoId, _) = await ArrangeAsync();

        var res = await r.Http.PostAsJsonAsync(ReplyUrl(appId, infoId), new { responseMessage = message });

        await AssertErrorAsync(res, HttpStatusCode.UnprocessableEntity, ResidentArcErrorCodes.ValidationError);
        Assert.Null(await WithDbAsync(db => db.ArchitecturalInfoRequests.Where(i => i.Id == infoId).Select(i => i.RespondedAt).SingleAsync()));
    }

    // Replies belong to open requests only: a closed request refuses them.
    [Fact]
    public async Task Reply_OnClosedApplication_Refused()
    {
        var r = await CreateResidentAsync();
        var asker = await CreateMemberAsync(r.CommunityId, CommunityRole.BoardMember);
        var appId = await SeedApplicationAsync(r, new AppSpec { Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Approved });
        var infoId = await SeedInfoRequestAsync(appId, asker.UserId, Question);

        var res = await r.Http.PostAsJsonAsync(ReplyUrl(appId, infoId), new { responseMessage = "Too late" });

        await AssertErrorAsync(res, HttpStatusCode.Conflict, ResidentArcErrorCodes.ApplicationClosed);
    }

    // FR-010 applies to reply files too.
    [Fact]
    public async Task ReplyAttachment_DisallowedType_Refused()
    {
        var (r, appId, infoId, _) = await ArrangeAsync();

        var res = await UploadAsync(r.Http, $"{Base}/{appId}/info-requests/{infoId}/attachments", ExeRenamedPdf, "survey.pdf");

        await AssertErrorAsync(res, HttpStatusCode.UnprocessableEntity, ResidentArcErrorCodes.UnsupportedFileType);
        Assert.False(await WithDbAsync(db => db.ArchitecturalAttachments.AnyAsync(a => a.InfoRequestId == infoId)));
    }
}
