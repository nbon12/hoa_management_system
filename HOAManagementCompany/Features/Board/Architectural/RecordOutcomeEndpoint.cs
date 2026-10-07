using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>
/// POST …/{applicationId}/outcome — the community manager records the outcome of a reached decision,
/// closing the application and emailing the owner (027 US6; FR-025/FR-026). Owner wording on a denial
/// follows the board's votes; the manager picks it only for a lapse (deemed) denial. With no owner
/// email on file the outcome is still recorded and the manager is told (analyze U2).
/// </summary>
public class RecordOutcomeEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    ArcQueries queries,
    ArcEmailRenderer renderer,
    ArcApplicationFactory factory,
    OutboxDispatcher dispatcher,
    ILogger<RecordOutcomeEndpoint> logger)
    : Endpoint<ArcOutcomeRequest, ArcDetailDto>
{
    public const int MaxTextLength = 2000;

    public override void Configure()
    {
        Post("/communities/{communityId}/architectural-applications/{applicationId}/outcome");
        Description(x => x.WithName("RecordArchitecturalOutcome").WithTags("Board").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(ArcOutcomeRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (!await scope.CanAccessAsync(User, req.CommunityId, CommunityCapability.ManageArchitecturalReview, ct))
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
        var reason = Clean(req.OwnerReason);
        var conditions = Clean(req.ConditionsOfApproval);
        var settings = await factory.GetOrCreateSettingsAsync(app.CommunityId, ct);

        var refusal = await ArcLocks.InLockedTransactionAsync(db, app, async () =>
        {
            if (app.Status == ArcApplicationStatus.Closed)
                return new ArcRefusal(StatusCodes.Status409Conflict, ArcErrorCodes.ApplicationClosed, "This application is already closed.");
            if (app.Status != ArcApplicationStatus.DecisionReached || app.DecisionOutcome is null)
                return new ArcRefusal(StatusCodes.Status409Conflict, ArcErrorCodes.NoDecision, "No decision has been reached yet.");

            var error = Validate(app, reason, conditions, req.Wording, out var wording);
            if (error is not null)
                return new ArcRefusal(StatusCodes.Status422UnprocessableEntity, ArcErrorCodes.ValidationError, error);

            if (app.DecisionOutcome == ArcOutcome.Denied)
            {
                app.OwnerReason = reason;
                app.DecisionWording = wording ?? app.DecisionWording ?? ArcDenialWording.RevisionsRequested;
            }
            else
            {
                app.ConditionsOfApproval = conditions;
            }
            app.Status = ArcApplicationStatus.Closed;
            app.ClosedAt = queries.Now;
            app.ClosedByUserId = caller;

            var owner = await db.Owners.FirstOrDefaultAsync(o => o.PropertyId == app.PropertyId, ct);
            if (owner is not null && !string.IsNullOrWhiteSpace(owner.Email))
            {
                var communityName = await db.Communities.Where(c => c.Id == app.CommunityId)
                    .Select(c => c.CommunityName).FirstAsync(ct);
                var message = renderer.OwnerOutcome(app, app.Property.Address, owner.FirstName, owner.Email,
                    communityName, settings.FormalDisapprovalStatement, DateOnly.FromDateTime(queries.Now.UtcDateTime));
                db.OutboxMessages.Add(ArcEmailRenderer.ToOutbox(
                    ArcEmailRenderer.OwnerKind(app.DecisionOutcome.Value, app.DecisionWording), message,
                    ArcQueries.OutcomeDedupPrefix(app.Id), ownerId: owner.Id, recipientUserId: null));
            }

            await db.SaveChangesAsync(ct);
            return null;
        }, ct);
        if (refusal is not null)
        {
            await ArcLocks.WriteAsync(HttpContext, refusal, ct);
            return;
        }

        ArcLog.OutcomeRecorded(logger, caller, req.CommunityId, app.Id,
            app.DecisionOutcome.ToString()!, app.DecisionWording?.ToString(), queries.Now);
        await dispatcher.DispatchPendingAsync(ct);
        await SendOkAsync(await queries.BuildDetailAsync(app, caller, ct), ct);
    }

    private static string? Validate(
        ArchitecturalApplication app, string? reason, string? conditions, string? wordingInput, out ArcDenialWording? wording)
    {
        wording = null;
        if (reason is { Length: > MaxTextLength } || conditions is { Length: > MaxTextLength })
            return $"Text must be at most {MaxTextLength} characters.";

        if (app.DecisionOutcome == ArcOutcome.Approved)
        {
            if (!string.IsNullOrWhiteSpace(wordingInput))
                return "wording applies only to denials.";
            return reason is not null ? "A reason applies only to denials." : null;
        }

        if (conditions is not null)
            return "Conditions of approval are not allowed on a denial.";
        if (reason is null)
            return "ownerReason is required for a denial: say what would need to change for approval.";

        if (!string.IsNullOrWhiteSpace(wordingInput))
        {
            if (app.DecisionSource != ArcDecisionSource.Lapse)
                return "wording comes from the board's votes and can only be chosen for a denial by default.";
            if (!Enum.TryParse(wordingInput.Trim(), ignoreCase: true, out ArcDenialWording parsed)
                || int.TryParse(wordingInput, out _) || !Enum.IsDefined(parsed))
                return "wording must be RevisionsRequested or Denied.";
            wording = parsed;
        }
        return null;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
