using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>T055 (US6) — revisions (FR-034/FR-035).</summary>
public class RevisionHistoryTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    // US6-S12: "Given application ARC-1042 was denied and its owner resubmitted a revision, When a board member opens the
    // revision, Then it is shown as "ARC-1042" with a "v2" badge, a new received date and due date, no votes, and a link to
    // v1 showing its decision, reason and board comments."
    [Fact]
    public async Task Revision_HasOwnDatesAndNoVotes_AndLinksToV1WithItsHistory()
    {
        var s = await CreateScenarioAsync(5);
        var v1 = await CreateApplicationAsync(s, new AppSpec
        {
            Number = 1042, Received = new DateOnly(2026, 4, 1), Status = ArcApplicationStatus.Closed,
            Outcome = ArcOutcome.Denied, Wording = ArcDenialWording.RevisionsRequested
        });
        await AddVotesAsync(v1, (s.Board[1], ArcVoteChoice.RevisionsNeeded));
        var v2 = await CreateApplicationAsync(s, new AppSpec
        {
            Number = 1042, Revision = 2, PreviousRevisionId = v1, Received = new DateOnly(2026, 5, 28)
        });
        await LoginAsAsync(s.Board[0]);

        var d2 = await DetailAsync(s.CommunityId, v2);
        Assert.Equal("ARC-1042", d2.DisplayId);
        Assert.Equal(2, d2.Revision);
        Assert.Equal(new DateOnly(2026, 5, 28), d2.ReceivedDate);
        Assert.Equal(new DateOnly(2026, 6, 27), d2.DueDate);
        Assert.Empty(d2.Votes);
        Assert.Equal([1, 2], d2.Revisions.Select(r => r.Revision));
        var link = d2.Revisions[0];
        Assert.Equal(v1, link.Id);
        Assert.Equal(new ArcDecisionDto("Denied", "RevisionsRequested", "Votes"), link.Decision);

        var d1 = await DetailAsync(s.CommunityId, v1);
        Assert.Single(d1.Votes);
    }

    // FR-034: removing an attachment from a revision never removes it from earlier revisions.
    [Fact]
    public async Task RemovingCarriedAttachmentFromV2_LeavesV1Untouched()
    {
        var s = await CreateScenarioAsync(3);
        var v1 = await CreateApplicationAsync(s, new AppSpec
        {
            Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Denied, Wording = ArcDenialWording.Denied,
            Attachments = [("plan.pdf", 10), ("photo.jpg", 20)]
        });

        Guid v2;
        using (var scope = NewScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ArcApplicationFactory>();
            var removeId = await Db(scope).ArchitecturalAttachments.Where(x => x.ApplicationId == v1 && x.FileName == "photo.jpg").Select(x => x.Id).SingleAsync();
            var app = await factory.CreateRevisionAsync(v1, new ArcNewApplication(
                s.PropertyId, "Praneeth Pattyam", ArcProjectType.Fence, "Fence v2", "Lower fence", Today, null, []), [removeId], default);
            await Db(scope).SaveChangesAsync();
            v2 = app.Id;
        }

        var v1Files = await WithDbAsync(db => db.ArchitecturalAttachments.Where(x => x.ApplicationId == v1).Select(x => x.FileName).OrderBy(x => x).ToListAsync());
        var v2Files = await WithDbAsync(db => db.ArchitecturalAttachments.Where(x => x.ApplicationId == v2).ToListAsync());
        Assert.Equal(["photo.jpg", "plan.pdf"], v1Files);
        var carried = Assert.Single(v2Files);
        Assert.Equal("plan.pdf", carried.FileName);
        var v1Key = await WithDbAsync(db => db.ArchitecturalAttachments.Where(x => x.ApplicationId == v1 && x.FileName == "plan.pdf").Select(x => x.StorageKey).SingleAsync());
        Assert.Equal(v1Key, carried.StorageKey);
    }
}
