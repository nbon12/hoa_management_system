using System.Security.Claims;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>
/// Shared read layer for architectural applications (027 tasks T016): eligibility and recusal,
/// batched tallies, the caller's vote state, overdue math in the community's time zone, and the
/// list/detail projections every endpoint returns.
/// </summary>
public sealed class ArcQueries(ApplicationDbContext db, TimeProvider clock)
{
    public DateTimeOffset Now => clock.GetUtcNow();

    public DateOnly TodayUtc => DateOnly.FromDateTime(Now.UtcDateTime);

    /// <summary>Active board-member memberships in the community (025 effective-permission rule).</summary>
    public IQueryable<CommunityMembership> ActiveBoard(Guid communityId)
    {
        var today = TodayUtc;
        return db.CommunityMemberships.Where(m =>
            m.CommunityId == communityId
            && m.Role == CommunityRole.BoardMember
            && m.Status == MembershipStatus.Active
            && (m.EndDate == null || m.EndDate >= today));
    }

    public async Task<HashSet<string>> ActiveBoardUserIdsAsync(Guid communityId, CancellationToken ct) =>
        (await ActiveBoard(communityId).Select(m => m.UserId).Distinct().ToListAsync(ct)).ToHashSet();

    /// <summary>Users linked to each property — a linked board member is recused (FR-017).</summary>
    public async Task<Dictionary<Guid, HashSet<string>>> LinkedUsersAsync(
        IEnumerable<Guid> propertyIds, CancellationToken ct)
    {
        var ids = propertyIds.Distinct().ToList();
        var rows = await db.UserProperties
            .Where(up => ids.Contains(up.PropertyId))
            .Select(up => new { up.PropertyId, up.UserId })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.PropertyId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.UserId).ToHashSet());
    }

    /// <summary>
    /// A board member linked to the property is recused (FR-017), unless recusal would leave nobody
    /// eligible to vote, e.g. a one-member board (031).
    /// </summary>
    public async Task<bool> IsRecusedAsync(string userId, Guid communityId, Guid propertyId, CancellationToken ct)
    {
        if (!await db.UserProperties.AnyAsync(up => up.UserId == userId && up.PropertyId == propertyId, ct))
            return false;
        var board = await ActiveBoardUserIdsAsync(communityId, ct);
        var linked = await LinkedUsersAsync([propertyId], ct);
        return !Eligible(board, linked.GetValueOrDefault(propertyId)).Contains(userId);
    }

    /// <summary>Eligible voters: active board members not linked to the property.</summary>
    public async Task<int> EligibleCountAsync(Guid communityId, Guid propertyId, CancellationToken ct)
    {
        var board = await ActiveBoardUserIdsAsync(communityId, ct);
        var linked = await LinkedUsersAsync([propertyId], ct);
        return Eligible(board, linked.GetValueOrDefault(propertyId)).Count;
    }

    /// <summary>True once the end of <paramref name="dueDate"/> has passed in the given time zone.</summary>
    public static bool IsDueDatePassed(DateOnly dueDate, string timeZoneId, DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, ResolveTimeZone(timeZoneId));
        return DateOnly.FromDateTime(local.DateTime) > dueDate;
    }

    public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(CommunityArcSettings.DefaultTimeZoneId);
        }
    }

    /// <summary>Loads an application with its property, only if it belongs to the community (FR-016).</summary>
    public Task<ArchitecturalApplication?> FindInCommunityAsync(
        Guid communityId, Guid applicationId, CancellationToken ct) =>
        db.ArchitecturalApplications
            .Include(a => a.Property)
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.CommunityId == communityId, ct);

    public async Task<IReadOnlyList<ArcListItemDto>> BuildItemsAsync(
        Guid communityId, IReadOnlyList<ArchitecturalApplication> apps, string callerId, CancellationToken ct)
    {
        if (apps.Count == 0)
            return [];

        var ids = apps.Select(a => a.Id).ToList();
        var board = await ActiveBoardUserIdsAsync(communityId, ct);
        var linked = await LinkedUsersAsync(apps.Select(a => a.PropertyId), ct);

        var votes = (await db.ArchitecturalVotes
                .Where(v => ids.Contains(v.ApplicationId))
                .Select(v => new { v.ApplicationId, v.VoterUserId, v.Choice })
                .ToListAsync(ct))
            .GroupBy(v => v.ApplicationId)
            .ToDictionary(g => g.Key, g => g.Select(v => (v.VoterUserId, v.Choice)).ToList());

        var attachmentCounts = await db.ArchitecturalAttachments
            .Where(x => ids.Contains(x.ApplicationId))
            .GroupBy(x => x.ApplicationId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var infoRequested = (await db.ArchitecturalInfoRequests
                .Where(r => ids.Contains(r.ApplicationId) && r.RespondedAt == null)
                .Select(r => r.ApplicationId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        var now = Now;
        return apps.Select(a =>
        {
            var appVotes = votes.GetValueOrDefault(a.Id) ?? [];
            var appLinked = linked.GetValueOrDefault(a.PropertyId);
            var eligible = Eligible(board, appLinked);
            var final = a.Status == ArcApplicationStatus.Closed || a.IsDecisionFinal(now);
            var myVote = MyVote(a, callerId, appVotes, board, eligible, now);
            return new ArcListItemDto(
                a.Id,
                a.DisplayId,
                a.Revision,
                a.Property.Address,
                a.OwnerName,
                a.ProjectTitle,
                attachmentCounts.GetValueOrDefault(a.Id),
                a.ReceivedDate,
                a.DueDate,
                IsOverdue(a, now),
                a.Status.ToString(),
                ToDecisionDto(a),
                infoRequested.Contains(a.Id),
                // 031: a final decision keeps the board size it was decided by.
                final && a.EligibleVoterCount is { } snapshot ? SnapshotTally(appVotes, snapshot) : Tally(appVotes, eligible),
                myVote,
                DecisionFinal: a.DecisionOutcome is not null && final,
                VotesLockAt: a.Status == ArcApplicationStatus.DecisionReached && !final ? a.DecisionFinalAt : null,
                CanUndoVote: myVote.State == ArcVoteStates.Voted && ArcVoting.VotesOpen(a, now));
        }).ToList();
    }

    public async Task<ArcListItemDto> BuildItemAsync(
        ArchitecturalApplication app, string callerId, CancellationToken ct) =>
        (await BuildItemsAsync(app.CommunityId, [app], callerId, ct))[0];

    public async Task<ArcDetailDto> BuildDetailAsync(
        ArchitecturalApplication app, string callerId, CancellationToken ct)
    {
        var item = await BuildItemAsync(app, callerId, ct);

        var attachments = await db.ArchitecturalAttachments
            .Where(x => x.ApplicationId == app.Id)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.FileName)
            .Select(x => new ArcAttachmentDto(x.Id, x.FileName, x.SizeBytes, x.ContentType, x.InfoRequestId, x.OwnerCommentId))
            .ToListAsync(ct);

        var ownerComments = (await db.ArchitecturalOwnerComments
                .Where(c => c.ApplicationId == app.Id)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new { c.Id, Name = c.Author.FirstName + " " + c.Author.LastName, c.Body, c.CreatedAt })
                .ToListAsync(ct))
            .Select(c => new ArcOwnerCommentDto(c.Id, c.Name, c.Body, c.CreatedAt,
                attachments.Where(x => x.OwnerCommentId == c.Id).ToList()))
            .ToList();

        var votes = await db.ArchitecturalVotes
            .Where(v => v.ApplicationId == app.Id)
            .OrderBy(v => v.CastAt)
            .Select(v => new { Name = v.Voter.FirstName + " " + v.Voter.LastName, v.Choice, v.Comment, v.CastAt })
            .ToListAsync(ct);

        var infoRequests = await db.ArchitecturalInfoRequests
            .Where(r => r.ApplicationId == app.Id)
            .OrderBy(r => r.RequestedAt)
            .Select(r => new ArcInfoRequestDto(
                r.Id, r.RequestedBy.FirstName + " " + r.RequestedBy.LastName, r.Message, r.RequestedAt, r.RespondedAt,
                r.ResponseMessage))
            .ToListAsync(ct);

        var revisions = (await db.ArchitecturalApplications
                .Where(a => a.CommunityId == app.CommunityId && a.ApplicationNumber == app.ApplicationNumber)
                .OrderBy(a => a.Revision)
                .ToListAsync(ct))
            .Select(a => new ArcRevisionSummaryDto(a.Id, a.Revision, a.ReceivedDate, ToDecisionDto(a)))
            .ToList();

        return new ArcDetailDto(
            item.Id, item.DisplayId, item.Revision, item.PropertyAddress, item.OwnerName, item.ProjectTitle,
            app.ProjectType.ToString(), app.Description, item.AttachmentCount, item.ReceivedDate, item.DueDate,
            item.Overdue, item.Status, item.Decision, item.InfoRequested, item.Tally, item.MyVote,
            attachments,
            votes.Select(v => new ArcVoteDto(v.Name, v.Choice.ToString(), v.Comment, v.CastAt)).ToList(),
            infoRequests,
            ArcDecisionRules.RuleText(app.DecisionRule, item.Tally.Eligible),
            app.ConditionsOfApproval,
            app.OwnerReason,
            app.ClosedAt,
            await OwnerEmailStatusAsync(app, ct),
            revisions,
            item.DecisionFinal,
            item.VotesLockAt,
            item.CanUndoVote,
            ownerComments,
            await LapseStatementAsync(app, ct));
    }

    /// <summary>
    /// 031: for a decision made by the lapse rule, the explanation shown where the vote card would be,
    /// citing the clause of the community's governing documents behind the rule.
    /// </summary>
    public async Task<string?> LapseStatementAsync(ArchitecturalApplication app, CancellationToken ct)
    {
        if (app.DecisionSource != ArcDecisionSource.Lapse || app.DecisionOutcome is not (ArcOutcome.Approved or ArcOutcome.Denied))
            return null;
        var citation = await db.CommunityArcSettings.Where(s => s.CommunityId == app.CommunityId)
                           .Select(s => s.LapseRuleCitation).FirstOrDefaultAsync(ct)
                       ?? CommunityArcSettings.DefaultLapseRuleCitation;
        return LapseStatement(app, citation);
    }

    public static string LapseStatement(ArchitecturalApplication app, string citation)
    {
        var approved = app.DecisionOutcome == ArcOutcome.Approved;
        var days = app.DueDate.DayNumber - app.ReceivedDate.DayNumber;
        return $"{(approved ? "Approved" : "Denied")} by default: the board did not approve or deny this application by its due date " +
               $"({app.DueDate:MM/dd/yy}). Under {citation}, an application the board does not act on within the {days}-day " +
               $"review period is deemed {(approved ? "approved" : "denied")}.";
    }

    /// <summary>
    /// Owner email status, always read from the outbox so it stays correct when the sweep delivers
    /// later (027 analyze U1). NoOwnerEmail when closed without a queued email (U2).
    /// </summary>
    public async Task<string?> OwnerEmailStatusAsync(ArchitecturalApplication app, CancellationToken ct)
    {
        var prefix = OutcomeDedupPrefix(app.Id);
        var latest = await db.OutboxMessages
            .Where(m => m.DedupKey != null && m.DedupKey.StartsWith(prefix))
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => (OutboxStatus?)m.Status)
            .FirstOrDefaultAsync(ct);

        if (latest is not null)
            return latest.Value.ToString();
        return app.Status == ArcApplicationStatus.Closed ? ArcOwnerEmailStatuses.NoOwnerEmail : null;
    }

    public static string OutcomeDedupPrefix(Guid applicationId) => $"arc:{applicationId}:outcome";

    public static ArcDecisionDto? ToDecisionDto(ArchitecturalApplication a) =>
        a.DecisionOutcome is null
            ? null
            : new ArcDecisionDto(
                a.DecisionOutcome.Value.ToString(),
                a.DecisionWording?.ToString(),
                // A resident withdrawal (029) has no decision source.
                a.DecisionOutcome == ArcOutcome.Withdrawn ? null : (a.DecisionSource ?? ArcDecisionSource.Votes).ToString());

    public static bool IsOverdue(ArchitecturalApplication a, DateTimeOffset now) =>
        a.Status == ArcApplicationStatus.Open && IsDueDatePassed(a.DueDate, a.TimeZoneId, now);

    public static string CallerId(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value ?? string.Empty;

    private static HashSet<string> Eligible(HashSet<string> board, HashSet<string>? linked)
    {
        if (linked is null)
            return board;
        var eligible = board.Where(id => !linked.Contains(id)).ToHashSet();
        // 031: recusal never leaves nobody able to vote — on a one-member board the member votes.
        return eligible.Count == 0 ? board : eligible;
    }

    private static ArcTallyDto SnapshotTally(List<(string VoterUserId, ArcVoteChoice Choice)> votes, int eligible) =>
        new(votes.Count(v => v.Choice == ArcVoteChoice.Approve),
            votes.Count(v => v.Choice == ArcVoteChoice.RevisionsNeeded),
            votes.Count(v => v.Choice == ArcVoteChoice.Deny),
            Math.Max(0, eligible - votes.Count),
            eligible);

    private static ArcTallyDto Tally(List<(string VoterUserId, ArcVoteChoice Choice)> votes, HashSet<string> eligible)
    {
        var voters = votes.Select(v => v.VoterUserId).ToHashSet();
        return new ArcTallyDto(
            votes.Count(v => v.Choice == ArcVoteChoice.Approve),
            votes.Count(v => v.Choice == ArcVoteChoice.RevisionsNeeded),
            votes.Count(v => v.Choice == ArcVoteChoice.Deny),
            eligible.Count(id => !voters.Contains(id)),
            eligible.Count);
    }

    private static ArcMyVoteDto MyVote(
        ArchitecturalApplication a,
        string callerId,
        List<(string VoterUserId, ArcVoteChoice Choice)> votes,
        HashSet<string> board,
        HashSet<string> eligible,
        DateTimeOffset now)
    {
        var mine = votes.FirstOrDefault(v => v.VoterUserId == callerId);
        if (mine.VoterUserId is not null)
            return new ArcMyVoteDto(ArcVoteStates.Voted, mine.Choice.ToString());
        if (!board.Contains(callerId))
            return new ArcMyVoteDto(ArcVoteStates.NotEligible);
        if (!eligible.Contains(callerId))
            return new ArcMyVoteDto(ArcVoteStates.Recused);
        // 031: a decision still in its grace period accepts votes from members who haven't voted.
        return ArcVoting.VotesOpen(a, now)
            ? new ArcMyVoteDto(ArcVoteStates.CanVote)
            : new ArcMyVoteDto(ArcVoteStates.NotEligible);
    }
}
