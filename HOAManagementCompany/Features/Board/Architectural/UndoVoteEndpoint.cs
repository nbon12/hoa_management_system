using FastEndpoints;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>
/// DELETE …/{applicationId}/votes/mine — undo the caller's vote so they can recast it (031). Allowed
/// while the application is undecided or its decision is still in its grace period; refused once the
/// decision is final or the application is closed. Runs under the same row lock as voting, and
/// re-runs the decision rule so a decision that loses its threshold reopens the application.
/// </summary>
public class UndoVoteEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    ArcQueries queries,
    ILogger<UndoVoteEndpoint> logger)
    : Endpoint<ArcApplicationRoute, ArcListItemDto>
{
    public override void Configure()
    {
        Delete("/communities/{communityId}/architectural-applications/{applicationId}/votes/mine");
        Description(x => x.WithName("UndoArchitecturalVote").WithTags("Board").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(ArcApplicationRoute req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (!await scope.CanAccessAsync(User, req.CommunityId, CommunityCapability.VoteArchitecturalApplications, ct))
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var app = await queries.FindInCommunityAsync(req.CommunityId, req.ApplicationId, ct);
        if (app is null)
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var caller = ArcQueries.CallerId(User);
        var refusal = await ArcLocks.InLockedTransactionAsync(db, app, async () =>
        {
            var statusRefusal = CastVoteEndpoint.StatusRefusal(app, queries.Now);
            if (statusRefusal is not null)
                return new ArcRefusal(StatusCodes.Status409Conflict, statusRefusal.Value.code, statusRefusal.Value.message);

            var vote = await db.ArchitecturalVotes.FirstOrDefaultAsync(v => v.ApplicationId == app.Id && v.VoterUserId == caller, ct);
            if (vote is null)
                return new ArcRefusal(StatusCodes.Status404NotFound, ArcErrorCodes.NotVoted, "You have not voted on this application.");

            db.ArchitecturalVotes.Remove(vote);
            await db.SaveChangesAsync(ct);
            await ArcVoting.ApplyVotesAsync(db, queries, app, ct);
            await db.SaveChangesAsync(ct);
            return null;
        }, ct);
        if (refusal is not null)
        {
            await ArcLocks.WriteAsync(HttpContext, refusal, ct);
            return;
        }

        ArcLog.VoteUndone(logger, caller, req.CommunityId, app.Id, queries.Now);
        await SendOkAsync(await queries.BuildItemAsync(app, caller, ct), ct);
    }
}
