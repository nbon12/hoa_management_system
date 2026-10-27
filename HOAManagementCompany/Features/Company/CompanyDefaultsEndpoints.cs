using System.Security.Claims;
using System.Text.Json;
using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Company;

public sealed record CompanyDefaultsDto(
    int ReviewPeriodDays, string LapseRule, string DecisionRule, int ReminderDays, string TimeZoneId,
    string FormalDisapprovalStatement, DateTimeOffset? UpdatedAt, bool CanEdit);

public sealed class CompanyDefaultsPutRequest
{
    public int ReviewPeriodDays { get; set; }
    public string? LapseRule { get; set; }
    public string? DecisionRule { get; set; }
    public int ReminderDays { get; set; }
    public string? TimeZoneId { get; set; }
    public string? FormalDisapprovalStatement { get; set; }
}

internal static class CompanyDefaults
{
    public static async Task<CompanyDefaultSettings> GetOrCreateAsync(ApplicationDbContext db, Guid companyId, CancellationToken ct)
    {
        var row = await db.CompanyDefaultSettings.FirstOrDefaultAsync(d => d.CompanyId == companyId, ct);
        if (row is not null)
            return row;
        row = new CompanyDefaultSettings { CompanyId = companyId };
        db.CompanyDefaultSettings.Add(row);
        await db.SaveChangesAsync(ct);
        return row;
    }

    // The six rules only, so the change log compares like with like.
    public static string Rules(CompanyDefaultSettings d) => JsonSerializer.Serialize(new
    {
        d.ReviewPeriodDays, LapseRule = d.LapseRule.ToString(), DecisionRule = d.DecisionRule.ToString(), d.ReminderDays,
        d.TimeZoneId, d.FormalDisapprovalStatement,
    });

    public static CompanyDefaultsDto ToDto(CompanyDefaultSettings d, bool canEdit, bool everEdited) => new(
        d.ReviewPeriodDays, d.LapseRule.ToString(), d.DecisionRule.ToString(), d.ReminderDays, d.TimeZoneId,
        d.FormalDisapprovalStatement, everEdited ? d.UpdatedAt : null, canEdit);
}

/// <summary>
/// GET /company/defaults — the ARC rules new communities copy (030 US4, FR-025). Company
/// Administrators and effective Community Managers read them; <c>canEdit</c> is a UX hint only.
/// </summary>
public class CompanyDefaultsGetEndpoint(ApplicationDbContext db, ICommunityScopeResolver scope, ICompanyContext companyContext)
    : EndpointWithoutRequest<CompanyDefaultsDto>
{
    public override void Configure()
    {
        Get("/company/defaults");
        Description(x => x.WithName("CompanyDefaults").WithTags("Company"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (!await scope.CanAccessCompanyAsync(User, CompanyCapability.ViewCompanyDefaults, ct)
            || await companyContext.ForUserAsync(User, ct) is not { } companyId)
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var row = await CompanyDefaults.GetOrCreateAsync(db, companyId, ct);
        var canEdit = await scope.CanAccessCompanyAsync(User, CompanyCapability.ManageCompanyDefaults, ct);
        await SendOkAsync(CompanyDefaults.ToDto(row, canEdit, row.UpdatedByUserId is not null), ct);
    }
}

/// <summary>
/// PUT /company/defaults — Company Administrators only. Changes only the defaults row: existing
/// communities and their applications are never touched (FR-026). Validated as the community
/// ARC settings are (R13), and logged with old and new values.
/// </summary>
public class CompanyDefaultsPutEndpoint(
    ApplicationDbContext db, ICommunityScopeResolver scope, ICompanyContext companyContext, TimeProvider clock,
    ILogger<CompanyDefaultsPutEndpoint> logger)
    : Endpoint<CompanyDefaultsPutRequest, CompanyDefaultsDto>
{
    public override void Configure()
    {
        Put("/company/defaults");
        Description(x => x.WithName("PutCompanyDefaults").WithTags("Company").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(CompanyDefaultsPutRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (!await scope.CanAccessCompanyAsync(User, CompanyCapability.ManageCompanyDefaults, ct)
            || await companyContext.ForUserAsync(User, ct) is not { } companyId)
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        if (ArcRuleValidator.Validate(req.ReviewPeriodDays, req.LapseRule, req.DecisionRule, req.ReminderDays,
                req.TimeZoneId, req.FormalDisapprovalStatement, out var rules) is { } error)
            throw CompanyErrors.Validation(new CompanyErrors.FieldError(error.Field, error.Message));

        var row = await CompanyDefaults.GetOrCreateAsync(db, companyId, ct);
        var before = CompanyDefaults.Rules(row);
        var now = clock.GetUtcNow();
        var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value;
        row.ReviewPeriodDays = rules.ReviewPeriodDays;
        row.LapseRule = rules.LapseRule;
        row.DecisionRule = rules.DecisionRule;
        row.ReminderDays = rules.ReminderDays;
        row.TimeZoneId = rules.TimeZoneId;
        row.FormalDisapprovalStatement = rules.FormalDisapprovalStatement;
        row.UpdatedAt = now;
        row.UpdatedByUserId = actorId;
        await db.SaveChangesAsync(ct);

        CompanyLog.DefaultsChanged(logger, actorId, companyId, before, CompanyDefaults.Rules(row), now);
        await SendOkAsync(CompanyDefaults.ToDto(row, canEdit: true, everEdited: true), ct);
    }
}
