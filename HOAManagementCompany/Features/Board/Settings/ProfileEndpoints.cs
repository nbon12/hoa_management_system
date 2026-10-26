using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using FastEndpoints;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HOAManagementCompany.Features.Board.Settings;

public sealed class ProfileRequest
{
    public Guid CommunityId { get; set; }
}

/// <summary>
/// Any subset of the six profile fields. Each is raw JSON so a field that was sent (even as
/// null, to clear it) can be told apart from one that wasn't.
/// </summary>
public sealed class ProfilePatchRequest
{
    public Guid CommunityId { get; set; }
    public JsonElement LegalName { get; set; }
    public JsonElement CommunityName { get; set; }
    public JsonElement County { get; set; }
    public JsonElement FormationDate { get; set; }
    public JsonElement ManagementStartDate { get; set; }
    public JsonElement Description { get; set; }
}

public sealed record ProfileDto(
    string LegalName, string CommunityName, string? County, DateOnly? FormationDate, DateOnly? ManagementStartDate,
    string? Description, IReadOnlyList<string> MissingRequiredFields)
{
    public static ProfileDto From(Domain.Entities.Community c) => new(c.LegalName, c.CommunityName, c.County,
        c.FormationDate, c.ManagementStartDate, c.Description, CommunityProfileFields.MissingRequired(c));
}

/// <summary>GET /communities/{communityId}/profile (030 FR-017).</summary>
public class ProfileGetEndpoint(ApplicationDbContext db, ICommunityScopeResolver scope) : Endpoint<ProfileRequest, ProfileDto>
{
    public override void Configure()
    {
        Get("/communities/{communityId}/profile");
        Description(x => x.WithName("CommunityProfile").WithTags("Community settings"));
    }

    public override async Task HandleAsync(ProfileRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (await BoardHttp.SendDecisionAsync(HttpContext, await scope.DecideAsync(User, req.CommunityId,
                CommunityCapability.ViewCommunitySettings, ScopeOperation.Read, ct), ct))
            return;
        await SendOkAsync(ProfileDto.From(await db.Communities.AsNoTracking().FirstAsync(c => c.Id == req.CommunityId, ct)), ct);
    }
}

/// <summary>
/// PATCH /communities/{communityId}/profile — saves the fields sent, one per request from the UI
/// (030 FR-017, R14). The FR-006 rules apply, except that a required field left empty by the 025
/// backfill may stay empty; once it has a value it can't be cleared. Names stay unique in the
/// company (R15). Each changed field logs one CommunityProfileChanged with its old and new value.
/// </summary>
public class ProfilePatchEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    TimeProvider clock,
    ILogger<ProfilePatchEndpoint> logger) : Endpoint<ProfilePatchRequest, ProfileDto>
{
    private const string NameIndex = "IX_Communities_ManagementCompanyId_NormalizedName";

    public override void Configure()
    {
        Patch("/communities/{communityId}/profile");
        Description(x => x.WithName("PatchCommunityProfile").WithTags("Community settings").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(ProfilePatchRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (await BoardHttp.SendDecisionAsync(HttpContext, await scope.DecideAsync(User, req.CommunityId,
                CommunityCapability.EditCommunityProfile, ScopeOperation.Write, ct), ct))
            return;

        var community = await db.Communities.FirstAsync(c => c.Id == req.CommunityId, ct);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var errors = new List<CompanyErrors.FieldError?>();
        var changes = new List<(string Field, string? Old, string? New, Action Apply)>();

        void Text(JsonElement value, string field, string? current, Func<string?, bool, CompanyErrors.FieldError?> rule,
            Action<string?> set, bool optional = false)
        {
            if (value.ValueKind == JsonValueKind.Undefined)
                return;
            if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            {
                errors.Add(new(field, "Enter text."));
                return;
            }
            var next = value.GetString()?.Trim();
            next = string.IsNullOrEmpty(next) ? null : next;
            // A field that has a value can't be cleared; a backfill gap may stay empty.
            var error = rule(next, !optional && !string.IsNullOrWhiteSpace(current));
            if (error is not null) { errors.Add(error); return; }
            if (next != current)
                changes.Add((field, current, next, () => set(next)));
        }

        void Date(JsonElement value, string field, DateOnly? current, Func<DateOnly?, bool, CompanyErrors.FieldError?> rule,
            Action<DateOnly?> set)
        {
            if (value.ValueKind == JsonValueKind.Undefined)
                return;
            DateOnly? next = null;
            if (value.ValueKind == JsonValueKind.String
                && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                next = parsed;
            else if (value.ValueKind != JsonValueKind.Null)
            {
                errors.Add(new(field, "Enter a real date as MM/DD/YYYY."));
                return;
            }
            var error = rule(next, current is not null);
            if (error is not null) { errors.Add(error); return; }
            if (next != current)
                changes.Add((field, current?.ToString("yyyy-MM-dd"), next?.ToString("yyyy-MM-dd"), () => set(next)));
        }

        Text(req.LegalName, "legalName", community.LegalName, CommunityFieldRules.LegalName, v => community.LegalName = v ?? string.Empty);
        Text(req.CommunityName, "communityName", community.CommunityName, CommunityFieldRules.CommunityName,
            v => community.CommunityName = v ?? string.Empty);
        Text(req.County, "county", community.County, CommunityFieldRules.County, v => community.County = v);
        Date(req.FormationDate, "formationDate", community.FormationDate,
            (v, required) => CommunityFieldRules.FormationDate(v, today, required), v => community.FormationDate = v);
        Date(req.ManagementStartDate, "managementStartDate", community.ManagementStartDate,
            CommunityFieldRules.ManagementStartDate, v => community.ManagementStartDate = v);
        Text(req.Description, "description", community.Description, (v, _) => CommunityFieldRules.Description(v),
            v => community.Description = v, optional: true);

        var failures = errors.OfType<CompanyErrors.FieldError>().ToArray();
        if (failures.Length > 0)
            throw CompanyErrors.Validation(failures);

        var isAdministrator = await scope.IsCompanyAdministratorAsync(User, community.ManagementCompanyId, ct);
        if (changes.Any(c => c.Field == "communityName"))
        {
            var normalized = Domain.Entities.Community.Normalize(changes.First(c => c.Field == "communityName").New!);
            if (await db.Communities.AsNoTracking()
                    .Where(c => c.ManagementCompanyId == community.ManagementCompanyId && c.Id != community.Id
                                && c.NormalizedName == normalized)
                    .Select(c => new { c.CommunityName, c.Status })
                    .FirstOrDefaultAsync(ct) is { } taken)
                throw CompanyErrors.CommunityNameTaken(isAdministrator ? taken.CommunityName : null,
                    taken.Status == CommunityStatus.Archived);
        }

        foreach (var change in changes)
            change.Apply();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: NameIndex })
        {
            throw CompanyErrors.CommunityNameTaken(null, otherIsArchived: false);
        }

        var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value;
        foreach (var change in changes)
            CompanyLog.ProfileChanged(logger, actorId, community.Id, char.ToUpperInvariant(change.Field[0]) + change.Field[1..],
                change.Old, change.New, now);
        await SendOkAsync(ProfileDto.From(community), ct);
    }
}
