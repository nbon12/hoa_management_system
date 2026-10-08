using System.Text.Json;
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
    public const int MaxStatementLength = 1000;

    public override void Configure()
    {
        Put("/communities/{communityId}/architectural-settings");
        Description(x => x.WithName("PutArchitecturalSettings").WithTags("Board").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(ArcSettingsPutRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (!await scope.CanAccessAsync(User, req.CommunityId, CommunityCapability.ManageArchitecturalReview, ct))
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var error = Validate(req, out var lapse, out var rule);
        if (error is not null)
        {
            await ArcHttp.ValidationAsync(HttpContext, error, ct);
            return;
        }

        var settings = await factory.GetOrCreateSettingsAsync(req.CommunityId, ct);
        var before = JsonSerializer.Serialize(ArcSettingsMapping.ToDto(settings, true) with { UpdatedAt = null });

        settings.ReviewPeriodDays = req.ReviewPeriodDays;
        settings.LapseRule = lapse;
        settings.DecisionRule = rule;
        settings.ReminderDays = req.ReminderDays;
        settings.TimeZoneId = req.TimeZoneId!.Trim();
        settings.FormalDisapprovalStatement = req.FormalDisapprovalStatement!.Trim();
        settings.UpdatedAt = queries.Now;
        settings.UpdatedByUserId = ArcQueries.CallerId(User);
        await db.SaveChangesAsync(ct);

        var dto = ArcSettingsMapping.ToDto(settings, true);
        ArcLog.SettingsChanged(logger, settings.UpdatedByUserId, req.CommunityId, before,
            JsonSerializer.Serialize(dto with { UpdatedAt = null }), queries.Now);
        await SendOkAsync(dto, ct);
    }

    private static string? Validate(ArcSettingsPutRequest req, out ArcLapseRule lapse, out ArcDecisionRule rule)
    {
        lapse = default;
        rule = default;
        if (req.ReviewPeriodDays is < 1 or > 365)
            return "reviewPeriodDays must be between 1 and 365.";
        if (req.ReminderDays is < 0 or > 30)
            return "reminderDays must be between 0 and 30 (0 turns reminders off).";
        if (!ParseEnum(req.LapseRule, out lapse))
            return "lapseRule must be FlagOverdueOnly, DeemedApproved or DeemedDenied.";
        if (!ParseEnum(req.DecisionRule, out rule))
            return "decisionRule must be MajorityOfMembers or MajorityOfVotesCastWithQuorum.";
        if (string.IsNullOrWhiteSpace(req.TimeZoneId) || !IsTimeZone(req.TimeZoneId.Trim()))
            return "timeZoneId must be a valid IANA time zone, e.g. America/New_York.";
        var statement = req.FormalDisapprovalStatement?.Trim();
        if (string.IsNullOrEmpty(statement))
            return "formalDisapprovalStatement is required.";
        if (statement.Length > MaxStatementLength)
            return $"formalDisapprovalStatement must be at most {MaxStatementLength} characters.";
        return null;
    }

    private static bool ParseEnum<T>(string? value, out T result) where T : struct, Enum
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value) && !int.TryParse(value, out _)
               && Enum.TryParse(value.Trim(), ignoreCase: true, out result) && Enum.IsDefined(result);
    }

    private static bool IsTimeZone(string id)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return false; }
    }
}
