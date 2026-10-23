using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>
/// PUT /communities/{communityId}/architectural-settings — community manager edits the review period,
/// lapse rule, decision rule, reminder days, time zone and formal disapproval statement (027 FR-029/
/// FR-030). Applies to applications received afterwards; logged with old and new values.
/// </summary>
public class ArcSettingsPutEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    ArcApplicationFactory factory,
    ArcQueries queries,
    ILogger<ArcSettingsPutEndpoint> logger)
    : Endpoint<ArcSettingsPutRequest, ArcSettingsDto>
{
    public const int MaxStatementLength = ArcRuleValidator.MaxStatementLength;

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

        var settings = await factory.GetOrCreateSettingsAsync(req.CommunityId, ct);
        var before = ArcSettingsSection.Rules(settings);

        settings.ReviewPeriodDays = rules.ReviewPeriodDays;
        settings.LapseRule = rules.LapseRule;
        settings.DecisionRule = rules.DecisionRule;
        settings.ReminderDays = rules.ReminderDays;
        settings.TimeZoneId = rules.TimeZoneId;
        settings.FormalDisapprovalStatement = rules.FormalDisapprovalStatement;
        settings.UpdatedAt = queries.Now;
        settings.UpdatedByUserId = ArcQueries.CallerId(User);
        await db.SaveChangesAsync(ct);

        var dto = ArcSettingsMapping.ToDto(settings, true);
        ArcLog.SettingsChanged(logger, settings.UpdatedByUserId, req.CommunityId, before, ArcSettingsSection.Rules(settings),
            queries.Now);
        await SendOkAsync(dto, ct);
    }
}
