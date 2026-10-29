using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>
/// POST …/{applicationId}/votes — cast Approve / RevisionsNeeded / Deny with an optional comment
/// (027 US2; FR-014–FR-018, FR-023). One vote per board member; recused owners and non-board roles are
/// refused. The vote and the decision check run in one transaction under a row lock so concurrent
/// deciding votes produce exactly one decision (SC-005, research R4). 031: votes are accepted until the
/// decision is final (the grace period after the threshold is met), see <see cref="ArcVoting"/>.
/// </summary>
public class CastVoteEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    ArcQueries queries,
    ILogger<CastVoteEndpoint> logger)
    : Endpoint<ArcVoteRequest, ArcListItemDto>
{
    public const int MaxCommentLength = 2000;

    public override void Configure()
    {
        Post("/communities/{communityId}/architectural-applications/{applicationId}/votes");
        Description(x => x.WithName("CastArchitecturalVote").WithTags("Board").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(ArcVoteRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        // 030 FR-004a: forbidden, archived or allowed, decided in one place.
        if (await BoardHttp.SendDecisionAsync(HttpContext,
                await scope.DecideAsync(User, req.CommunityId, CommunityCapability.VoteArchitecturalApplications, ScopeOperation.Write, ct), ct))
            return;

        if (!TryParseChoice(req.Choice, out var choice))
        {
            await ArcHttp.ValidationAsync(HttpContext, "choice must be Approve, RevisionsNeeded or Deny.", ct);
            return;
        }
        var comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim();
        if (comment is { Length: > MaxCommentLength })
        {
            await ArcHttp.ValidationAsync(HttpContext, $"comment must be at most {MaxCommentLength} characters.", ct);
            return;
        }

        var app = await queries.FindInCommunityAsync(req.CommunityId, req.ApplicationId, ct);
        if (app is null)
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var caller = ArcQueries.CallerId(User);
        if (await queries.IsRecusedAsync(caller, app.CommunityId, app.PropertyId, ct))
        {
            await ArcHttp.ErrorAsync(HttpContext, StatusCodes.Status403Forbidden, ArcErrorCodes.Recused,
                "You own this property, so you are recused from voting on it.", ct);
            return;
        }

        var refusal = await ArcLocks.InLockedTransactionAsync(db, app, async () =>
        {
            var statusRefusal = StatusRefusal(app, queries.Now);
            if (statusRefusal is not null)
                return new ArcRefusal(StatusCodes.Status409Conflict, statusRefusal.Value.code, statusRefusal.Value.message);
            if (await db.ArchitecturalVotes.AnyAsync(v => v.ApplicationId == app.Id && v.VoterUserId == caller, ct))
                return AlreadyVoted;

            var vote = new ArchitecturalVote
            {
                ApplicationId = app.Id,
                VoterUserId = caller,
                Choice = choice,
                Comment = comment,
                CastAt = queries.Now
            };
            db.ArchitecturalVotes.Add(vote);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                db.Entry(vote).State = EntityState.Detached;
                return AlreadyVoted;
            }

            await ArcVoting.ApplyVotesAsync(db, queries, app, ct);
            await db.SaveChangesAsync(ct);
            return null;
        }, ct);
        if (refusal is not null)
        {
            await ArcLocks.WriteAsync(HttpContext, refusal, ct);
            return;
        }

        ArcLog.VoteCast(logger, caller, req.CommunityId, app.Id, choice.ToString(), queries.Now);
        await SendAsync(await queries.BuildItemAsync(app, caller, ct), StatusCodes.Status201Created, ct);
    }

    private static readonly ArcRefusal AlreadyVoted =
        new(StatusCodes.Status409Conflict, ArcErrorCodes.AlreadyVoted, "You have already voted on this application.");

    /// <summary>Questions are only for undecided applications (027 FR-021).</summary>
    internal static (string code, string message)? StatusRefusal(ArcApplicationStatus status) => status switch
    {
        ArcApplicationStatus.Closed => (ArcErrorCodes.ApplicationClosed, "This application is closed."),
        ArcApplicationStatus.DecisionReached => (ArcErrorCodes.ApplicationDecided, "A decision has already been reached on this application."),
        _ => null
    };

    internal static (string code, string message)? StatusRefusal(ArchitecturalApplication app, DateTimeOffset now) =>
        app.Status switch
        {
            ArcApplicationStatus.Closed => (ArcErrorCodes.ApplicationClosed, "This application is closed."),
            ArcApplicationStatus.DecisionReached when app.IsDecisionFinal(now) =>
                (ArcErrorCodes.ApplicationDecided, "The board's decision on this application is final."),
            _ => null
        };

    private static bool TryParseChoice(string? value, out ArcVoteChoice choice)
    {
        choice = default;
        return !string.IsNullOrWhiteSpace(value)
               && !int.TryParse(value, out _)
               && Enum.TryParse(value.Trim(), ignoreCase: true, out choice)
               && Enum.IsDefined(choice);
    }
}
