using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Features.Property.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Property.Architectural;

/// <summary>029 User Story 4 — Withdraw a request (T054), spec.md US4 AS1–AS2 and Clarifications 2026-10-08.</summary>
public class WithdrawTests(TestDatabaseFixture fixture) : ResidentArcTestBase(fixture)
{
    private static string WithdrawUrl(Guid id) => $"{Base}/{id}/withdraw";

    // US4 AS1 / FR-020: Given a submitted, undecided request, When the resident withdraws it, Then it moves to
    // Closed with outcome "withdrawn", leaves the board's Open list, is excluded from the board's default Closed
    // view, and is visible to the board only with the "show withdrawn" filter.
    [Fact]
    public async Task Withdraw_Undecided_ClosesAsWithdrawn_HiddenFromBoardByDefault()
    {
        var r = await CreateResidentAsync();
        var (_, board) = await CreateBoardClientAsync(r.CommunityId);
        var app = await SubmitNewAsync(r);
        var approvedId = await SeedApplicationAsync(r, new AppSpec { Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Approved });

        var res = await r.Http.PostAsync(WithdrawUrl(app.Id), null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(ResidentArcStatuses.Withdrawn, (await ReadAsync<ResidentArcDetailDto>(res)).Status);
        var row = await WithDbAsync(db => db.ArchitecturalApplications.SingleAsync(a => a.Id == app.Id));
        Assert.Equal(ArcApplicationStatus.Closed, row.Status);
        Assert.Equal(ArcOutcome.Withdrawn, row.DecisionOutcome);
        Assert.Equal(r.UserId, row.WithdrawnByUserId);
        Assert.NotNull(row.WithdrawnAt);

        var open = await board.GetFromJsonAsync<ArcListResponse>($"{AppsUrl(r.CommunityId)}?status=open", Json);
        var closedDefault = await board.GetFromJsonAsync<ArcListResponse>($"{AppsUrl(r.CommunityId)}?status=closed", Json);
        var closedAll = await board.GetFromJsonAsync<ArcListResponse>($"{AppsUrl(r.CommunityId)}?status=closed&includeWithdrawn=true", Json);
        Assert.DoesNotContain(open!.Items, i => i.Id == app.Id);
        Assert.Equal(approvedId, Assert.Single(closedDefault!.Items).Id);
        Assert.Equal(1, closedDefault.Counts.Closed);
        Assert.Equal(2, closedAll!.Items.Count);
        Assert.Equal(2, closedAll.Counts.Closed);
        Assert.Equal("Withdrawn", Assert.Single(closedAll.Items, i => i.Id == app.Id).Decision!.Outcome);

        // Withdrawal sends no email: the only outbox row for this request is the submission confirmation.
        var keys = await WithDbAsync(db => db.OutboxMessages.Where(m => m.DedupKey!.StartsWith($"arc:{app.Id}:"))
            .Select(m => m.DedupKey).ToListAsync());
        Assert.Equal([ResidentArcSubmitService.SubmittedDedupKey(app.Id)], keys);
    }

    // US4 AS2 / FR-021: a request the board has already decided can't be withdrawn; nothing changes.
    [Fact]
    public async Task Withdraw_DecisionReached_Refused()
    {
        var r = await CreateResidentAsync();
        var id = await SeedApplicationAsync(r, new AppSpec { Status = ArcApplicationStatus.DecisionReached, Outcome = ArcOutcome.Approved });

        var res = await r.Http.PostAsync(WithdrawUrl(id), null);

        await AssertErrorAsync(res, HttpStatusCode.Conflict, ResidentArcErrorCodes.ApplicationDecided);
        var row = await WithDbAsync(db => db.ArchitecturalApplications.SingleAsync(a => a.Id == id));
        Assert.Equal(ArcApplicationStatus.DecisionReached, row.Status);
        Assert.Equal(ArcOutcome.Approved, row.DecisionOutcome);
        Assert.Null(row.WithdrawnAt);
    }

    // US4 AS2 / FR-021 / SC-008: Approved, Denied and already-Withdrawn requests can't be withdrawn.
    [Theory]
    [InlineData(ArcOutcome.Approved)]
    [InlineData(ArcOutcome.Denied)]
    [InlineData(ArcOutcome.Withdrawn)]
    public async Task Withdraw_Closed_Refused(ArcOutcome outcome)
    {
        var r = await CreateResidentAsync();
        var id = await SeedApplicationAsync(r, new AppSpec { Status = ArcApplicationStatus.Closed, Outcome = outcome });

        var res = await r.Http.PostAsync(WithdrawUrl(id), null);

        await AssertErrorAsync(res, HttpStatusCode.Conflict, ResidentArcErrorCodes.ApplicationClosed);
        var row = await WithDbAsync(db => db.ArchitecturalApplications.SingleAsync(a => a.Id == id));
        Assert.Equal(ArcApplicationStatus.Closed, row.Status);
        Assert.Equal(outcome, row.DecisionOutcome);
        Assert.Null(row.WithdrawnAt);
    }

    // FR-021 under concurrency: a withdrawal racing the deciding vote never yields both. Under 027's row lock,
    // exactly one of them wins and the other is refused.
    [Fact]
    public async Task Withdraw_RacingDecidingVote_ExactlyOneWins()
    {
        var r = await CreateResidentAsync();
        var (m1, _) = await CreateBoardClientAsync(r.CommunityId);
        var (_, voter2) = await CreateBoardClientAsync(r.CommunityId);
        await CreateBoardClientAsync(r.CommunityId);
        var app = await SubmitNewAsync(r); // majority of members: 2 of 3 decide
        await AddVotesAsync(app.Id, (m1, ArcVoteChoice.Approve));

        var withdrawTask = r.Http.PostAsync(WithdrawUrl(app.Id), null);
        var voteTask = voter2.PostAsJsonAsync($"{AppsUrl(r.CommunityId)}/{app.Id}/votes", new { choice = "Approve" });
        await Task.WhenAll(withdrawTask, voteTask);
        var (withdraw, vote) = (withdrawTask.Result, voteTask.Result);

        var row = await WithDbAsync(db => db.ArchitecturalApplications.SingleAsync(a => a.Id == app.Id));
        if (row.DecisionOutcome == ArcOutcome.Withdrawn)
        {
            Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);
            await AssertErrorAsync(vote, HttpStatusCode.Conflict, ArcErrorCodes.ApplicationClosed);
            Assert.Equal(ArcApplicationStatus.Closed, row.Status);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Created, vote.StatusCode);
            await AssertErrorAsync(withdraw, HttpStatusCode.Conflict, ArcErrorCodes.ApplicationDecided);
            Assert.Equal(ArcApplicationStatus.DecisionReached, row.Status);
            Assert.Equal(ArcOutcome.Approved, row.DecisionOutcome);
            Assert.Null(row.WithdrawnAt);
        }
    }

    // A withdrawn request is out of 027's sweep: no reminder or lapse, even once it passes its due date.
    [Fact]
    public async Task Sweep_IgnoresWithdrawn()
    {
        var r = await CreateResidentAsync();
        await CreateBoardClientAsync(r.CommunityId);
        var app = await SubmitNewAsync(r);
        await r.Http.PostAsync(WithdrawUrl(app.Id), null);
        Clock.UtcNow = Clock.UtcNow.AddDays(60);

        using var sweep = new HttpRequestMessage(HttpMethod.Post, "/api/v1/architectural/jobs/sweep");
        sweep.Headers.Add("X-Scheduler-Secret", "test-scheduler-shared-secret-placeholder");
        var res = await CreateClient().SendAsync(sweep);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var row = await WithDbAsync(db => db.ArchitecturalApplications.SingleAsync(a => a.Id == app.Id));
        Assert.Equal(ArcOutcome.Withdrawn, row.DecisionOutcome);
        Assert.Null(row.ReminderSentAt);
        Assert.Null(row.LapseProcessedAt);
        Assert.False(await WithDbAsync(db => db.OutboxMessages.AnyAsync(m =>
            m.DedupKey!.StartsWith($"arc:{app.Id}:reminder") || m.DedupKey!.StartsWith($"arc:{app.Id}:lapse"))));
    }
}
