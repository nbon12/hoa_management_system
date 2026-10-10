using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Property.Architectural;

/// <summary>
/// 029 FR-022–FR-024 / SC-006 and the authorization edge cases (T040): only owners of the property — on
/// their active property — can act on its requests; co-owners have equal access; a board role never widens
/// resident access; and every refusal is the same non-disclosing 403.
/// </summary>
public class ResidentArcAuthorizationTests(TestDatabaseFixture fixture) : ResidentArcTestBase(fixture)
{
    private sealed record Targets(Guid DraftId, Guid DraftAttachmentId, Guid OpenAppId, Guid OpenAttachmentId,
        Guid InfoRequestId, Guid DeniedAppId);

    /// <summary>The owner's draft (with a file), an open application (with a file and an open question) and a denial.</summary>
    private async Task<(Resident Owner, Targets T)> ArrangeAsync()
    {
        var owner = await CreateResidentAsync();
        var asker = await CreateMemberAsync(owner.CommunityId, CommunityRole.BoardMember);
        var draft = await CreateDraftAsync(owner);
        var draftFile = await ReadAsync<Features.Property.Architectural.ResidentArcAttachmentDto>(
            await UploadAsync(owner.Http, $"{Base}/drafts/{draft.Id}/attachments", ValidPdf, "plan.pdf"));
        var open = await SubmitNewAsync(owner, withPdf: true);
        var info = await SeedInfoRequestAsync(open.Id, asker.UserId, "Please attach a plat survey");
        var denied = await SeedApplicationAsync(owner, new AppSpec
        {
            Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Denied, Wording = ArcDenialWording.Denied
        });
        return (owner, new Targets(draft.Id, draftFile.Id, open.Id, Assert.Single(open.Attachments).Id, info, denied));
    }

    private static Task<HttpResponseMessage> CallAsync(HttpClient http, string route, Targets t) => route switch
    {
        "GET draft" => http.GetAsync($"{Base}/drafts/{t.DraftId}"),
        "PUT draft" => http.PutAsJsonAsync($"{Base}/drafts/{t.DraftId}", CompleteDraft()),
        "DELETE draft" => http.DeleteAsync($"{Base}/drafts/{t.DraftId}"),
        "POST draft file" => UploadAsync(http, $"{Base}/drafts/{t.DraftId}/attachments", ValidPdf, "x.pdf"),
        "DELETE draft file" => http.DeleteAsync($"{Base}/drafts/{t.DraftId}/attachments/{t.DraftAttachmentId}"),
        "GET draft file url" => http.GetAsync($"{Base}/drafts/{t.DraftId}/attachments/{t.DraftAttachmentId}/url"),
        "POST submit" => http.PostAsync($"{Base}/drafts/{t.DraftId}/submit", null),
        "GET detail" => http.GetAsync($"{Base}/{t.OpenAppId}"),
        "GET file url" => http.GetAsync($"{Base}/{t.OpenAppId}/attachments/{t.OpenAttachmentId}/url"),
        "POST reply file" => UploadAsync(http, $"{Base}/{t.OpenAppId}/info-requests/{t.InfoRequestId}/attachments", ValidPdf, "x.pdf"),
        "POST reply" => http.PostAsJsonAsync($"{Base}/{t.OpenAppId}/info-requests/{t.InfoRequestId}/reply", new { responseMessage = "hi" }),
        "POST withdraw" => http.PostAsync($"{Base}/{t.OpenAppId}/withdraw", null),
        "POST revise" => http.PostAsync($"{Base}/{t.DeniedAppId}/revise", null),
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };

    public static TheoryData<string> Routes => new()
    {
        "GET draft", "PUT draft", "DELETE draft", "POST draft file", "DELETE draft file", "GET draft file url", "POST submit",
        "GET detail", "GET file url", "POST reply file", "POST reply", "POST withdraw", "POST revise"
    };

    // FR-022 / SC-006: a resident whose active property is a different property is refused on EVERY route with
    // the exact contract body, and nothing the owner holds changes.
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task NonOwner_EveryEndpoint_Forbidden(string route)
    {
        var (owner, t) = await ArrangeAsync();
        var stranger = await CreateResidentAsync(owner.CommunityId);

        var res = await CallAsync(stranger.Http, route, t);

        await AssertForbiddenAsync(res);
        await AssertUntouchedAsync(t);
    }

    // FR-022 / constitution §7: an unknown id gets the same 403 body as someone else's, so existence never leaks.
    [Theory]
    [InlineData("draft")]
    [InlineData("application")]
    public async Task Forbidden_BodyIdentical_ForNonexistentId(string kind)
    {
        var r = await CreateResidentAsync();
        var url = kind == "draft" ? $"{Base}/drafts/{Guid.NewGuid()}" : $"{Base}/{Guid.NewGuid()}";

        await AssertForbiddenAsync(await r.Http.GetAsync(url));
    }

    // FR-022 / edge case: a co-owner (a second UserProperty on the same property) sees and acts on a request
    // another owner created.
    [Fact]
    public async Task CoOwner_CanViewWithdrawReplyAndRevise()
    {
        var (owner, t) = await ArrangeAsync();
        var coOwner = await AddCoOwnerAsync(owner);

        var view = await coOwner.Http.GetAsync($"{Base}/{t.OpenAppId}");
        var reply = await coOwner.Http.PostAsJsonAsync($"{Base}/{t.OpenAppId}/info-requests/{t.InfoRequestId}/reply",
            new { responseMessage = "Survey attached" });
        var withdraw = await coOwner.Http.PostAsync($"{Base}/{t.OpenAppId}/withdraw", null);
        var revise = await coOwner.Http.PostAsync($"{Base}/{t.DeniedAppId}/revise", null);
        var draft = await coOwner.Http.GetAsync($"{Base}/drafts/{t.DraftId}");

        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);
        Assert.Equal(HttpStatusCode.Created, revise.StatusCode);
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        var app = await WithDbAsync(db => db.ArchitecturalApplications.SingleAsync(a => a.Id == t.OpenAppId));
        Assert.Equal(ArcOutcome.Withdrawn, app.DecisionOutcome);
        Assert.Equal(coOwner.UserId, app.WithdrawnByUserId);
    }

    // FR-023 / edge case: a board member (and a manager) of the SAME community who doesn't own the property gets
    // the resident 403 — a board role never widens resident access.
    [Theory]
    [InlineData(CommunityRole.BoardMember)]
    [InlineData(CommunityRole.CommunityManager)]
    public async Task BoardMemberOfCommunity_WhoIsNotOwner_Forbidden(CommunityRole role)
    {
        var (owner, t) = await ArrangeAsync();
        var (_, boardHttp) = await CreateBoardClientAsync(owner.CommunityId, role);

        await AssertForbiddenAsync(await boardHttp.GetAsync($"{Base}/{t.OpenAppId}"));
        await AssertForbiddenAsync(await boardHttp.PostAsync($"{Base}/{t.OpenAppId}/withdraw", null));
        await AssertForbiddenAsync(await boardHttp.GetAsync($"{Base}/drafts/{t.DraftId}"));
        Assert.DoesNotContain(t.OpenAppId.ToString(), await boardHttp.GetStringAsync(Base));
        await AssertUntouchedAsync(t);
    }

    // FR-022 + 025 FR-015: the list follows the ACTIVE property; switching property re-scopes it.
    [Fact]
    public async Task SwitchingActiveProperty_RescopesList()
    {
        var r = await CreateResidentAsync();
        var onA = await SubmitNewAsync(r);
        Guid propertyB;
        using (var scope = NewScope())
            propertyB = await CreatePropertyAsync(Db(scope), r.CommunityId, address: "12 Other St");
        await LinkUserToPropertyAsync(r.UserId, propertyB);

        var toB = await SwitchAsync(r.Http, propertyB);
        var listOnB = await MyListAsync(new Resident(r.UserId, r.Email, propertyB, r.CommunityId, toB));
        var detailFromB = await toB.GetAsync($"{Base}/{onA.Id}");
        var backToA = await SwitchAsync(toB, r.PropertyId);
        var listOnA = await MyListAsync(r with { Http = backToA });

        Assert.Empty(listOnB.Items);
        await AssertForbiddenAsync(detailFromB);
        Assert.Equal(onA.Id, Assert.Single(listOnA.Items).Id);
    }

    // FR-024: a resident of another community is refused with the same 403.
    [Fact]
    public async Task CrossCommunity_Forbidden()
    {
        var (_, t) = await ArrangeAsync();
        var elsewhere = await CreateResidentAsync(); // a brand-new community

        await AssertForbiddenAsync(await elsewhere.Http.GetAsync($"{Base}/{t.OpenAppId}"));
        await AssertForbiddenAsync(await elsewhere.Http.GetAsync($"{Base}/drafts/{t.DraftId}"));
    }

    // Constitution §7: a denied access is a logged sensitive event (IDs only).
    [Fact]
    public async Task Denial_LogsSensitiveEvent()
    {
        var (owner, t) = await ArrangeAsync();
        var stranger = await CreateResidentAsync(owner.CommunityId);

        await stranger.Http.GetAsync($"{Base}/{t.OpenAppId}");

        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcResidentAccessDenied")
            && e.Properties["ActorId"].ToString().Contains(stranger.UserId)
            && e.Properties["Resource"].ToString().Contains(t.OpenAppId.ToString()));
    }

    private async Task<HttpClient> SwitchAsync(HttpClient http, Guid propertyId)
    {
        var res = await http.PostAsJsonAsync("/api/v1/auth/switch-property", new { propertyId });
        res.EnsureSuccessStatusCode();
        var token = (await res.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>())!["token"].GetString()!;
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task AssertUntouchedAsync(Targets t)
    {
        var draft = await WithDbAsync(db => db.ArchitecturalApplicationDrafts.Include(d => d.Attachments)
            .SingleOrDefaultAsync(d => d.Id == t.DraftId));
        Assert.NotNull(draft);
        Assert.Equal("Fence replacement — 6ft cedar", draft!.ProjectTitle);
        Assert.Equal(t.DraftAttachmentId, Assert.Single(draft.Attachments).Id);
        var open = await WithDbAsync(db => db.ArchitecturalApplications.Include(a => a.Attachments).Include(a => a.InfoRequests)
            .SingleAsync(a => a.Id == t.OpenAppId));
        Assert.Equal(ArcApplicationStatus.Open, open.Status);
        Assert.Single(open.Attachments);
        Assert.Null(Assert.Single(open.InfoRequests).RespondedAt);
        Assert.False(await WithDbAsync(db => db.ArchitecturalApplicationDrafts.AnyAsync(d => d.PreviousRevisionId == t.DeniedAppId)));
    }
}
