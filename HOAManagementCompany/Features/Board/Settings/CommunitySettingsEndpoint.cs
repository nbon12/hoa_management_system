using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Settings;

public sealed class CommunitySettingsRequest
{
    public Guid CommunityId { get; set; }
}

/// <summary>A status action and, when it isn't allowed, the code that says why (for the disabled control).</summary>
public sealed record SettingsAction(bool Allowed, string? ReasonCode);

public sealed record SettingsCan(
    bool EditProfile,
    bool ChangeParent,
    SettingsAction Activate,
    SettingsAction Archive,
    SettingsAction Restore,
    bool ManageManagers,
    bool ReplaceLastManager,
    bool ManageMemberships,
    bool ManageResidents,
    bool EditArcSettings,
    bool EndMembershipsWhileArchived);

public sealed record ParentSummary(Guid Id, string CommunityName);

public sealed record CommunitySettingsDto(
    Guid Id,
    string CommunityName,
    string LegalName,
    string Status,
    ParentSummary? Parent,
    bool? NoManager,
    IReadOnlyList<string> MissingRequiredFields,
    IReadOnlyList<string> Sections,
    SettingsCan Can);

/// <summary>
/// FR-006 and FR-017: the profile fields a community must have. A field still empty from the
/// spec 025 backfill may stay empty, and the Profile section flags it.
/// </summary>
public static class CommunityProfileFields
{
    public static IReadOnlyList<string> MissingRequired(Domain.Entities.Community community)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(community.LegalName)) missing.Add("legalName");
        if (string.IsNullOrWhiteSpace(community.CommunityName)) missing.Add("communityName");
        if (string.IsNullOrWhiteSpace(community.County)) missing.Add("county");
        if (community.FormationDate is null) missing.Add("formationDate");
        if (community.ManagementStartDate is null) missing.Add("managementStartDate");
        return missing;
    }
}

/// <summary>
/// GET /communities/{communityId}/settings — the Community settings page shell (030 US5; FR-010,
/// FR-029): the header, the sections, and what the caller may do. <c>can.*</c> drives the UI
/// only; every action re-checks on the server.
/// </summary>
public class CommunitySettingsEndpoint(ApplicationDbContext db, ICommunityScopeResolver scope, TimeProvider clock)
    : Endpoint<CommunitySettingsRequest, CommunitySettingsDto>
{
    // FR-029: Notifications is not shown until the Notification Settings spec lands.
    private static readonly string[] Sections = ["profile", "managers-and-memberships", "architectural-review"];

    public override void Configure()
    {
        Get("/communities/{communityId}/settings");
        Description(x => x.WithName("CommunitySettings").WithTags("Community settings"));
    }

    public override async Task HandleAsync(CommunitySettingsRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        var decision = await scope.DecideAsync(User, req.CommunityId, CommunityCapability.ViewCommunitySettings,
            ScopeOperation.Read, ct);
        if (await BoardHttp.SendDecisionAsync(HttpContext, decision, ct))
            return;

        var community = await db.Communities.AsNoTracking().FirstAsync(c => c.Id == req.CommunityId, ct);
        var parent = community.ParentCommunityId is { } parentId
            ? await db.Communities.AsNoTracking().Where(c => c.Id == parentId)
                .Select(c => new { c.Id, c.CommunityName, c.Status }).FirstAsync(ct)
            : null;

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var managers = await db.CommunityMemberships.AsNoTracking()
            .Where(m => m.CommunityId == community.Id && m.Role == CommunityRole.CommunityManager)
            .Select(m => new ManagerRule.Row(m.Id, m.Status, m.EndDate))
            .ToListAsync(ct);
        var hasEffectiveManager = managers.Any(m => m.Status == MembershipStatus.Active && (m.EndDate is null || m.EndDate >= today));
        var isCompanyAdministrator = await scope.IsCompanyAdministratorAsync(User, community.ManagementCompanyId, ct);

        async Task<bool> MayWrite(CommunityCapability capability) =>
            await scope.DecideAsync(User, community.Id, capability, ScopeOperation.Write, ct) == ScopeDecision.Allowed;

        var lifecycle = isCompanyAdministrator && await MayWrite(CommunityCapability.ManageCommunityLifecycle);
        var archived = community.Status == CommunityStatus.Archived;

        var activate = !lifecycle || community.Status != CommunityStatus.Onboarding
            ? new SettingsAction(false, null)
            : ManagerRule.HasOpenEndedManager(managers)
                ? new SettingsAction(true, null)
                : new SettingsAction(false, "NO_ACTIVE_MANAGER");
        var restore = !lifecycle || !archived
            ? new SettingsAction(false, null)
            : parent is { Status: CommunityStatus.Archived }
                ? new SettingsAction(false, "PARENT_ARCHIVED")
                : new SettingsAction(true, null);

        var can = new SettingsCan(
            EditProfile: await MayWrite(CommunityCapability.EditCommunityProfile),
            ChangeParent: lifecycle && !archived,
            Activate: activate,
            Archive: new SettingsAction(lifecycle && !archived, null),
            Restore: restore,
            ManageManagers: await MayWrite(CommunityCapability.ManageCommunityManagers),
            ReplaceLastManager: await MayWrite(CommunityCapability.ReplaceLastManager),
            ManageMemberships: await MayWrite(CommunityCapability.ManageMemberships),
            ManageResidents: await MayWrite(CommunityCapability.ManageResidents),
            EditArcSettings: await MayWrite(CommunityCapability.ManageArcSettings),
            EndMembershipsWhileArchived: lifecycle && archived);

        await SendOkAsync(new CommunitySettingsDto(
            community.Id,
            community.CommunityName,
            community.LegalName,
            community.Status.ToString(),
            parent is null ? null : new ParentSummary(parent.Id, parent.CommunityName),
            // Shown only to Company Administrators (contract).
            isCompanyAdministrator ? !hasEffectiveManager : null,
            CommunityProfileFields.MissingRequired(community),
            Sections,
            can), ct);
    }
}
