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
/// deciding votes produce exactly one decision (SC-005, research R4).
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
        if (!await scope.CanAccessAsync(User, req.CommunityId, CommunityCapability.VoteArchitecturalApplications, ct))
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

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
        if (await queries.IsRecusedAsync(caller, app.PropertyId, ct))
        {
            await ArcHttp.ErrorAsync(HttpContext, StatusCodes.Status403Forbidden, ArcErrorCodes.Recused,
                "You own this property, so you are recused from voting on it.", ct);
            return;
        }

        var refusal = await ArcLocks.InLockedTransactionAsync(db, app, async () =>
        {
            var statusRefusal = StatusRefusal(app.Status);
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

            var choices = await db.ArchitecturalVotes.Where(v => v.ApplicationId == app.Id).Select(v => v.Choice).ToListAsync(ct);
            var eligible = await queries.EligibleCountAsync(app.CommunityId, app.PropertyId, ct);
            var decision = ArcDecisionRules.Evaluate(
                app.DecisionRule, eligible,
                choices.Count(c => c == ArcVoteChoice.Approve),
                choices.Count(c => c == ArcVoteChoice.RevisionsNeeded),
                choices.Count(c => c == ArcVoteChoice.Deny),
                dueDatePassed: false);
            if (decision is not null)
            {
                ArcSweepService.SetDecision(app, decision, ArcDecisionSource.Votes, queries.Now);
                await db.SaveChangesAsync(ct);
            }
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

    internal static (string code, string message)? StatusRefusal(ArcApplicationStatus status) => status switch
    {
        ArcApplicationStatus.Closed => (ArcErrorCodes.ApplicationClosed, "This application is closed."),
        ArcApplicationStatus.DecisionReached => (ArcErrorCodes.ApplicationDecided, "A decision has already been reached on this application."),
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
