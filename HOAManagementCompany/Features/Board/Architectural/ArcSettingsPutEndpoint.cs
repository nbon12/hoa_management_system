using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>
/// PUT /communities/{communityId}/architectural-settings — community manager edits the review period,
/// lapse rule, decision rule, reminder days, time zone and formal disapproval statement (027 FR-029/
/// FR-030). Applies to applications received afterwards; logged with old and new values. Answers with
/// the GET body, as the reset does, so the Architectural review section shows the save without a reload.
/// </summary>
public class ArcSettingsPutEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    ArcApplicationFactory factory,
    ArcQueries queries,
    ILogger<ArcSettingsPutEndpoint> logger)
    : Endpoint<ArcSettingsPutRequest, ArcSettingsSectionDto>
{
    public const int MaxStatementLength = ArcRuleValidator.MaxStatementLength;
    public const int MaxCitationLength = 300;

    public override void Configure()
    {
        Put("/communities/{communityId}/architectural-settings");
        Description(x => x.WithName("PutArchitecturalSettings").WithTags("Board").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(ArcSettingsPutRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        // 030 FR-030: Community Managers and Company Administrators; COMMUNITY_ARCHIVED while archived.
        if (await BoardHttp.SendDecisionAsync(HttpContext,
                await scope.DecideAsync(User, req.CommunityId, CommunityCapability.ManageArcSettings, ScopeOperation.Write, ct), ct))
            return;

        var error = ArcRuleValidator.Validate(req.ReviewPeriodDays, req.LapseRule, req.DecisionRule, req.ReminderDays,
            req.TimeZoneId, req.FormalDisapprovalStatement, out var rules);
        if (error is not null)
        {
            await ArcHttp.ValidationAsync(HttpContext, error.Message, ct);
            return;
        }
        var citation = req.LapseRuleCitation?.Trim();
        if (citation is { Length: > MaxCitationLength })
        {
            await ArcHttp.ValidationAsync(HttpContext, $"lapseRuleCitation must be at most {MaxCitationLength} characters.", ct);
            return;
        }

        var settings = await factory.GetOrCreateSettingsAsync(req.CommunityId, ct);
        var before = ArcSettingsSection.Rules(settings);

        settings.ReviewPeriodDays = rules.ReviewPeriodDays;
        settings.LapseRule = rules.LapseRule;
        settings.DecisionRule = rules.DecisionRule;
        settings.ReminderDays = rules.ReminderDays;
        settings.TimeZoneId = rules.TimeZoneId;
        settings.FormalDisapprovalStatement = rules.FormalDisapprovalStatement;
        // 031: optional, so clients that don't send it keep the current citation.
        if (!string.IsNullOrEmpty(citation))
            settings.LapseRuleCitation = citation;
        settings.UpdatedAt = queries.Now;
        settings.UpdatedByUserId = ArcQueries.CallerId(User);
        await db.SaveChangesAsync(ct);

        ArcLog.SettingsChanged(logger, settings.UpdatedByUserId, req.CommunityId, before, ArcSettingsSection.Rules(settings),
            queries.Now);
        await SendOkAsync(await ArcSettingsSection.BuildAsync(db, scope, User, req.CommunityId, ct), ct);
    }
}
