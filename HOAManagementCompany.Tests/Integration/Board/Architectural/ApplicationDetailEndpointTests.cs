using System.Net;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>T038 (US3) — GET …/{applicationId}.</summary>
public class ApplicationDetailEndpointTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    // US3-S1: "Given application ARC-1042 was received on 05/28/26 from "Praneeth Pattyam" with attachments
    // fence-plan.pdf (1.2 MB), elevation.jpg (840 KB) and plat-survey.pdf (2.1 MB), When I select it, Then the
    // detail panel is titled "ARC-1042 · fence replacement" and shows the owner, the received date, and all
    // three files with their sizes."
    [Fact]
    public async Task Detail_ShowsOwnerReceivedDateAndAttachmentsWithSizes()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s, new AppSpec
        {
            Number = 1042,
            Received = new DateOnly(2026, 5, 28),
            Attachments = [("fence-plan.pdf", 1_258_291), ("elevation.jpg", 860_160), ("plat-survey.pdf", 2_202_009)]
        });
        await LoginAsAsync(s.Board[0]);

        var d = await DetailAsync(s.CommunityId, appId);

        Assert.Equal("ARC-1042", d.DisplayId);
        Assert.Equal("Fence replacement — 6ft cedar", d.ProjectTitle);
        Assert.Equal("Praneeth Pattyam", d.OwnerName);
        Assert.Equal(new DateOnly(2026, 5, 28), d.ReceivedDate);
        Assert.Equal(
            new[] { ("elevation.jpg", 860_160L), ("fence-plan.pdf", 1_258_291L), ("plat-survey.pdf", 2_202_009L) },
            d.Attachments.Select(a => (a.FileName, a.SizeBytes)).OrderBy(x => x.FileName));
    }

    // US3-S5: "Given the application has no attachments, When I select it, Then the panel says there are no
    // attachments instead of showing an empty list." (backend: empty array; UI text covered in the panel spec)
    [Fact]
    public async Task Detail_WithoutAttachments_HasEmptyAttachmentList()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s);
        await LoginAsAsync(s.Board[0]);
        Assert.Empty((await DetailAsync(s.CommunityId, appId)).Attachments);
    }

    // US3-S6: "Given any page in this feature, When it renders an attachment, Then the page does not contain a
    // durable public object URL for that file." List and detail responses carry no URL and no storage key.
    [Fact]
    public async Task ListAndDetail_NeverContainObjectUrlsOrStorageKeys()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s, new AppSpec { Attachments = [("fence-plan.pdf", 10)] });
        await LoginAsAsync(s.Board[0]);

        var list = await (await Client.GetAsync(AppsUrl(s.CommunityId))).Content.ReadAsStringAsync();
        var detail = await (await Client.GetAsync(AppUrl(s.CommunityId, appId))).Content.ReadAsStringAsync();

        foreach (var body in new[] { list, detail })
        {
            Assert.DoesNotContain("http://", body);
            Assert.DoesNotContain("https://", body);
            Assert.DoesNotContain($"arc/{s.CommunityId}", body);
        }
    }

    // FR-013 rule text, FR-019 comments visible to board, 025 FR-017 sensitive access event.
    [Fact]
    public async Task Detail_HasRuleText_Votes_AndLogsSensitiveAccess()
    {
        var s = await CreateScenarioAsync(5);
        var appId = await CreateApplicationAsync(s);
        await LoginAsAsync(s.Board[1]);
        (await VoteAsync(s.CommunityId, appId, "Deny", "Too tall")).EnsureSuccessStatusCode();
        await LoginAsAsync(s.Board[0]);

        var d = await DetailAsync(s.CommunityId, appId);

        Assert.Equal("Three of five votes decide. The manager records the outcome and notifies the owner.", d.RuleText);
        Assert.Equal("Too tall", Assert.Single(d.Votes).Comment);
        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("ArcSensitiveAccess")
            && e.Properties["Resource"].ToString().Contains($"application:{appId}"));
    }

    [Fact]
    public async Task Detail_InAnotherCommunity_IsRefused()
    {
        var a = await CreateScenarioAsync(1);
        var b = await CreateScenarioAsync(1);
        var appInB = await CreateApplicationAsync(b);
        await LoginAsAsync(a.Board[0]);

        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync(AppUrl(a.CommunityId, appInB))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync(AppUrl(b.CommunityId, appInB))).StatusCode);
    }

    [Fact]
    public async Task Manager_CanReadDetail_ButIsNotEligibleToVote()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s);
        var manager = await CreateMemberAsync(s.CommunityId, CommunityRole.CommunityManager);
        await LoginAsAsync(manager);

        Assert.Equal("NotEligible", (await DetailAsync(s.CommunityId, appId)).MyVote.State);
    }
}
