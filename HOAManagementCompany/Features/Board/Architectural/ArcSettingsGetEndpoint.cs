using System.Text.Json;
using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Architectural;

/// <summary>The six rules as the company defaults hold them (030 FR-027).</summary>
public sealed record ArcRulesDto(
    int ReviewPeriodDays, string LapseRule, string DecisionRule, int ReminderDays, string TimeZoneId,
    string FormalDisapprovalStatement);

/// <summary>
/// The Architectural review section: 027's settings, plus the company defaults they can be reset
/// to, which fields differ from them, and whether the caller may save (a UX hint only).
/// </summary>
public sealed record ArcSettingsSectionDto(
    int ReviewPeriodDays, string LapseRule, string DecisionRule, int ReminderDays, string TimeZoneId,
    string FormalDisapprovalStatement, DateTimeOffset? UpdatedAt, ArcRulesDto CompanyDefaults,
    IReadOnlyList<string> DiffersFromDefaults, bool CanEdit,
    // 031: the governing-document clause cited on lapse decisions (not part of the company defaults).
    string LapseRuleCitation = CommunityArcSettings.DefaultLapseRuleCitation);

internal static class ArcSettingsSection
{
    public static async Task<ArcSettingsSectionDto> BuildAsync(ApplicationDbContext db, ICommunityScopeResolver scope,
        System.Security.Claims.ClaimsPrincipal user, Guid communityId, CancellationToken ct)
    {
        var row = await db.CommunityArcSettings.AsNoTracking().FirstOrDefaultAsync(s => s.CommunityId == communityId, ct);
        var settings = row ?? CommunityArcSettings.Defaults(communityId);
        var dto = ArcSettingsMapping.ToDto(settings, row is not null);
        var defaults = await DefaultsForAsync(db, communityId, ct);
        var canEdit = await scope.DecideAsync(user, communityId, CommunityCapability.ManageArcSettings, ScopeOperation.Write, ct)
                      == ScopeDecision.Allowed;
        return new ArcSettingsSectionDto(dto.ReviewPeriodDays, dto.LapseRule, dto.DecisionRule, dto.ReminderDays, dto.TimeZoneId,
            dto.FormalDisapprovalStatement, dto.UpdatedAt,
            new ArcRulesDto(defaults.ReviewPeriodDays, defaults.LapseRule.ToString(), defaults.DecisionRule.ToString(),
                defaults.ReminderDays, defaults.TimeZoneId, defaults.FormalDisapprovalStatement),
            Differences(defaults, settings), canEdit, settings.LapseRuleCitation);
    }

    /// <summary>The community's company defaults; the built-in ones if the company has no row yet.</summary>
    public static async Task<CompanyDefaultSettings> DefaultsForAsync(ApplicationDbContext db, Guid communityId, CancellationToken ct) =>
        await db.Communities.AsNoTracking().Where(c => c.Id == communityId)
            .SelectMany(c => db.CompanyDefaultSettings.Where(d => d.CompanyId == c.ManagementCompanyId))
            .FirstOrDefaultAsync(ct)
        ?? new CompanyDefaultSettings();

    private static List<string> Differences(CompanyDefaultSettings d, CommunityArcSettings s)
    {
        var fields = new List<string>();
        if (d.ReviewPeriodDays != s.ReviewPeriodDays) fields.Add("reviewPeriodDays");
        if (d.LapseRule != s.LapseRule) fields.Add("lapseRule");
        if (d.DecisionRule != s.DecisionRule) fields.Add("decisionRule");
        if (d.ReminderDays != s.ReminderDays) fields.Add("reminderDays");
        if (d.TimeZoneId != s.TimeZoneId) fields.Add("timeZoneId");
        if (d.FormalDisapprovalStatement != s.FormalDisapprovalStatement) fields.Add("formalDisapprovalStatement");
        return fields;
    }

    public static string Rules(CommunityArcSettings settings) =>
        JsonSerializer.Serialize(ArcSettingsMapping.ToDto(settings, true) with { UpdatedAt = null });
}

/// <summary>
/// GET /communities/{communityId}/architectural-settings — the community's ARC rules (027 FR-029),
/// read by Board Members, Community Managers and Company Administrators (030 FR-030). Returns
/// defaults without writing when no row exists yet (analyze A2).
/// </summary>
public class ArcSettingsGetEndpoint(ApplicationDbContext db, ICommunityScopeResolver scope)
    : Endpoint<ArcSettingsRoute, ArcSettingsSectionDto>
{
    public override void Configure()
    {
        Get("/communities/{communityId}/architectural-settings");
        Description(x => x.WithName("GetArchitecturalSettings").WithTags("Board"));
    }

    public override async Task HandleAsync(ArcSettingsRoute req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (await BoardHttp.SendDecisionAsync(HttpContext,
                await scope.DecideAsync(User, req.CommunityId, CommunityCapability.ViewArcSettings, ScopeOperation.Read, ct), ct))
            return;

        await SendOkAsync(await ArcSettingsSection.BuildAsync(db, scope, User, req.CommunityId, ct), ct);
    }
}

/// <summary>
/// POST /communities/{communityId}/architectural-settings/reset — copies the six rules from the
/// company's current defaults (030 FR-031). <c>NextApplicationNumber</c> and existing applications
/// are untouched. Logged as a settings change with <c>Cause=reset</c>.
/// </summary>
public class ArcSettingsResetEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    ArcApplicationFactory factory,
    ArcQueries queries,
    ILogger<ArcSettingsResetEndpoint> logger)
    : EndpointWithoutRequest<ArcSettingsSectionDto>
{
    public override void Configure()
    {
        Post("/communities/{communityId}/architectural-settings/reset");
        Description(x => x.WithName("ResetArchitecturalSettings").WithTags("Board").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        var communityId = Route<Guid>("communityId");
        if (await BoardHttp.SendDecisionAsync(HttpContext,
                await scope.DecideAsync(User, communityId, CommunityCapability.ManageArcSettings, ScopeOperation.Write, ct), ct))
            return;

        var defaults = await ArcSettingsSection.DefaultsForAsync(db, communityId, ct);
        var settings = await factory.GetOrCreateSettingsAsync(communityId, ct);
        var before = ArcSettingsSection.Rules(settings);
        settings.ReviewPeriodDays = defaults.ReviewPeriodDays;
        settings.LapseRule = defaults.LapseRule;
        settings.DecisionRule = defaults.DecisionRule;
        settings.ReminderDays = defaults.ReminderDays;
        settings.TimeZoneId = defaults.TimeZoneId;
        settings.FormalDisapprovalStatement = defaults.FormalDisapprovalStatement;
        settings.UpdatedAt = queries.Now;
        settings.UpdatedByUserId = ArcQueries.CallerId(User);
        await db.SaveChangesAsync(ct);

        ArcLog.SettingsChanged(logger, settings.UpdatedByUserId, communityId, before, ArcSettingsSection.Rules(settings),
            queries.Now, cause: "reset");
        await SendOkAsync(await ArcSettingsSection.BuildAsync(db, scope, User, communityId, ct), ct);
    }
}
