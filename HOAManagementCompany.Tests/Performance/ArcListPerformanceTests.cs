using System.Diagnostics;
using System.Net;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Board.Architectural;
using Xunit;

namespace HOAManagementCompany.Tests.Performance;

/// <summary>
/// T073 — SC-006: "The applications page shows the first page of results within 2 seconds for a community with
/// 500 applications." 500 applications × 5 votes, bulk-inserted; the first list page is timed (median of 5).
/// </summary>
public class ArcListPerformanceTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    [Fact]
    public async Task FirstPage_For500Applications_IsUnder2Seconds()
    {
        var s = await CreateScenarioAsync(5);
        await WithDbAsync(async db =>
        {
            for (var i = 0; i < 500; i++)
            {
                var app = new ArchitecturalApplication
                {
                    Id = Guid.NewGuid(), CommunityId = s.CommunityId, PropertyId = s.PropertyId,
                    ApplicationNumber = 10_000 + i, OwnerName = $"Owner {i}", ProjectType = ArcProjectType.Fence,
                    ProjectTitle = $"Project {i}", ReceivedDate = Today.AddDays(-i % 60), DueDate = Today.AddDays(30 - i % 60),
                    DecisionRule = ArcDecisionRule.MajorityOfMembers, LapseRule = ArcLapseRule.FlagOverdueOnly,
                    Status = i % 3 == 0 ? ArcApplicationStatus.Closed : ArcApplicationStatus.Open,
                    DecisionOutcome = i % 3 == 0 ? ArcOutcome.Approved : null,
                };
                db.ArchitecturalApplications.Add(app);
                foreach (var member in s.Board.Take(i % 3 == 0 ? 5 : 2))
                    db.ArchitecturalVotes.Add(new ArchitecturalVote { ApplicationId = app.Id, VoterUserId = member.UserId, Choice = ArcVoteChoice.Approve });
            }
            return await db.SaveChangesAsync();
        });
        await LoginAsAsync(s.Board[0]);
        (await Client.GetAsync(AppsUrl(s.CommunityId))).EnsureSuccessStatusCode(); // warm-up

        var times = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            var sw = Stopwatch.StartNew();
            var res = await Client.GetAsync(AppsUrl(s.CommunityId));
            sw.Stop();
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            times.Add(sw.ElapsedMilliseconds);
        }
        times.Sort();
        Assert.True(times[2] < 2000, $"median first-page time {times[2]}ms (threshold 2000ms)");
    }
}
