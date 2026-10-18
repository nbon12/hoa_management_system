using System.Net.Mail;
using System.Security.Claims;
using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Settings;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Features.Invitations;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Residents;

// 030 US8 (FR-024–FR-024g; contracts/community-settings.md § Residents). All five routes use
// ManageResidents: reads are allowed to Company Administrators while the community is Archived,
// writes are refused with COMMUNITY_ARCHIVED. A property of another community gets the same 403
// as a community the caller can't reach.

public sealed class ResidentsQuery
{
    public Guid CommunityId { get; set; }
    public string? Search { get; set; }
    public string? Filter { get; set; }
    public int? Limit { get; set; }
    public int? Offset { get; set; }
}

public sealed record InvitationSummary(int Scheduled, int Pending, int Expired);

public sealed record ResidentRow(
    Guid PropertyId, string Address, IReadOnlyList<string> ResidentNames, string? MatchedEmail, InvitationSummary InvitationSummary);

public sealed record ResidentCounts(int All, int NoResident, int InvitationPending, int InvitationExpired);

public sealed record ResidentsListResponse(
    IReadOnlyList<ResidentRow> Items, int Total, int Limit, int Offset, ResidentCounts Counts, bool PropertiesLoaded);

public sealed record ResidentDto(string UserId, string Name, string? Email, DateOnly ResidentSince);

public sealed record PropertyDrawerDto(
    Guid PropertyId, string Address, IReadOnlyList<ResidentDto> Residents, IReadOnlyList<InvitationItem> Invitations,
    string TimeZoneId, bool CanInvite);

public sealed class ResidentInviteRequest
{
    public Guid CommunityId { get; set; }
    public Guid? PropertyId { get; set; }
    public string? Email { get; set; }
}

internal static class ResidentsHttp
{
    public const int MaxSearchLength = 100;

    public static string Actor(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")!.Value;

    public static Task<bool> InCommunityAsync(ApplicationDbContext db, Guid communityId, Guid propertyId, CancellationToken ct) =>
        db.Properties.AnyAsync(p => p.Id == propertyId && p.CommunityId == communityId, ct);
}

/// <summary>GET /communities/{communityId}/residents — one row per property, in address order.</summary>
public class ResidentsListEndpoint(
    ApplicationDbContext db, ICommunityScopeResolver scope, TimeProvider clock, ILogger<ResidentsListEndpoint> logger)
    : Endpoint<ResidentsQuery, ResidentsListResponse>
{
    public override void Configure()
    {
        Get("/communities/{communityId}/residents");
        Description(x => x.WithName("CommunityResidents").WithTags("Community settings"));
    }

    public override async Task HandleAsync(ResidentsQuery req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (await BoardHttp.SendDecisionAsync(HttpContext,
                await scope.DecideAsync(User, req.CommunityId, CommunityCapability.ManageResidents, ScopeOperation.Read, ct), ct))
            return;

        var search = string.IsNullOrWhiteSpace(req.Search) ? null : req.Search.Trim().ToLowerInvariant();
        if (search is { Length: > ResidentsHttp.MaxSearchLength })
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("search",
                $"Search can be at most {ResidentsHttp.MaxSearchLength} characters."));
        if (req.Filter is not (null or "" or "noResident" or "invitationPending" or "invitationExpired"))
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("filter",
                "filter must be noResident, invitationPending or invitationExpired."));
        var (limit, offset) = Paging.Normalize(req.Limit, req.Offset);
        var now = clock.GetUtcNow();

        var properties = db.Properties.AsNoTracking().Where(p => p.CommunityId == req.CommunityId);
        var residency = db.Invitations.Where(i => i.Kind == InvitationKind.Residency && i.CommunityId == req.CommunityId);

        // The counts ignore the search and the filter, so the filter chips always show the whole community.
        var counts = new ResidentCounts(
            await properties.CountAsync(ct),
            await properties.CountAsync(p => !db.UserProperties.Any(up => up.PropertyId == p.Id), ct),
            await properties.CountAsync(p => residency.Any(i => i.PropertyId == p.Id && (i.State == InvitationState.Scheduled
                || (i.State == InvitationState.Pending && i.ExpiresAt > now))), ct),
            await properties.CountAsync(p => residency.Any(i => i.PropertyId == p.Id
                && i.State == InvitationState.Pending && i.ExpiresAt <= now), ct));

        var filtered = properties;
        if (search is not null)
            filtered = filtered.Where(p => p.Address.ToLower().Contains(search)
                || db.UserProperties.Any(up => up.PropertyId == p.Id
                    && ((up.User.FirstName + " " + up.User.LastName).ToLower().Contains(search)
                        || up.User.Email!.ToLower().Contains(search))));
        filtered = req.Filter switch
        {
            "noResident" => filtered.Where(p => !db.UserProperties.Any(up => up.PropertyId == p.Id)),
            "invitationPending" => filtered.Where(p => residency.Any(i => i.PropertyId == p.Id
                && (i.State == InvitationState.Scheduled || (i.State == InvitationState.Pending && i.ExpiresAt > now)))),
            "invitationExpired" => filtered.Where(p => residency.Any(i => i.PropertyId == p.Id
                && i.State == InvitationState.Pending && i.ExpiresAt <= now)),
            _ => filtered,
        };

        var total = await filtered.CountAsync(ct);
        var rows = await filtered
            .OrderBy(p => EF.Functions.Collate(p.Address, "natural_sort"))
            .ThenBy(p => p.Id)
            .Skip(offset)
            .Take(limit)
            .Select(p => new
            {
                p.Id,
                p.Address,
                Residents = db.UserProperties.Where(up => up.PropertyId == p.Id).OrderBy(up => up.LinkedAt)
                    .Select(up => new { Name = (up.User.FirstName + " " + up.User.LastName).Trim(), up.User.Email })
                    .ToList(),
                Scheduled = residency.Count(i => i.PropertyId == p.Id && i.State == InvitationState.Scheduled),
                Pending = residency.Count(i => i.PropertyId == p.Id && i.State == InvitationState.Pending && i.ExpiresAt > now),
                Expired = residency.Count(i => i.PropertyId == p.Id && i.State == InvitationState.Pending && i.ExpiresAt <= now),
            })
            .ToListAsync(ct);

        CompanyLog.AssociationDataAccess(logger, ResidentsHttp.Actor(User), req.CommunityId, "communities/residents", now);
        var items = rows.Select(r => new ResidentRow(r.Id, r.Address, r.Residents.Select(x => x.Name).ToList(),
            search is null ? null : r.Residents.Select(x => x.Email).FirstOrDefault(e => e != null && e.ToLowerInvariant().Contains(search)),
            new InvitationSummary(r.Scheduled, r.Pending, r.Expired))).ToList();
        await SendOkAsync(new ResidentsListResponse(items, total, limit, offset, counts, counts.All > 0), ct);
    }
}

/// <summary>GET /communities/{communityId}/residents/{propertyId} — the property drawer.</summary>
public class ResidentDetailEndpoint(
    ApplicationDbContext db, ICommunityScopeResolver scope, TimeProvider clock, ILogger<ResidentDetailEndpoint> logger)
    : EndpointWithoutRequest<PropertyDrawerDto>
{
    public override void Configure()
    {
        Get("/communities/{communityId}/residents/{propertyId}");
        Description(x => x.WithName("CommunityResidentProperty").WithTags("Community settings"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        var communityId = Route<Guid>("communityId");
        var propertyId = Route<Guid>("propertyId");
        var decision = await scope.DecideAsync(User, communityId, CommunityCapability.ManageResidents, ScopeOperation.Read, ct);
        if (decision == ScopeDecision.Allowed && !await ResidentsHttp.InCommunityAsync(db, communityId, propertyId, ct))
            decision = ScopeDecision.Forbidden;
        if (await BoardHttp.SendDecisionAsync(HttpContext, decision, ct))
            return;

        var now = clock.GetUtcNow();
        var timeZoneId = await InvitationItems.TimeZoneAsync(db, communityId, ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var address = await db.Properties.Where(p => p.Id == propertyId).Select(p => p.Address).FirstAsync(ct);
        var residents = (await db.UserProperties.AsNoTracking().Where(up => up.PropertyId == propertyId)
                .OrderBy(up => up.LinkedAt)
                .Select(up => new { up.UserId, Name = (up.User.FirstName + " " + up.User.LastName).Trim(), up.User.Email, up.LinkedAt })
                .ToListAsync(ct))
            .Select(r => new ResidentDto(r.UserId, r.Name, r.Email,
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(r.LinkedAt, zone).DateTime)))
            .ToList();
        var invitations = await db.Invitations.AsNoTracking()
            .Where(i => i.Kind == InvitationKind.Residency && i.PropertyId == propertyId && i.State != InvitationState.Accepted)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);
        var canWrite = await scope.DecideAsync(User, communityId, CommunityCapability.ManageResidents, ScopeOperation.Write, ct)
                       == ScopeDecision.Allowed;
        var items = await InvitationItems.MapAsync(db, invitations, now, _ => canWrite, ct);

        CompanyLog.AssociationDataAccess(logger, ResidentsHttp.Actor(User), communityId, "communities/residents/property", now);
        await SendOkAsync(new PropertyDrawerDto(propertyId, address, residents, items, timeZoneId, canWrite), ct);
    }
}

/// <summary>
/// POST /communities/{communityId}/residents/invitations — Scheduled in an Onboarding community,
/// otherwise Pending and sent at once. The response doesn't say whether the email has an account.
/// </summary>
public class ResidentInviteEndpoint(
    ApplicationDbContext db, ICommunityScopeResolver scope, ICompanyContext companyContext, InvitationService invitations)
    : Endpoint<ResidentInviteRequest, MembershipInvitationResponse>
{
    public override void Configure()
    {
        Post("/communities/{communityId}/residents/invitations");
        Description(x => x.WithName("InviteResident").WithTags("Community settings").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(ResidentInviteRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        var decision = await scope.DecideAsync(User, req.CommunityId, CommunityCapability.ManageResidents, ScopeOperation.Write, ct);
        if (decision == ScopeDecision.Allowed && req.PropertyId is { } requested
            && !await ResidentsHttp.InCommunityAsync(db, req.CommunityId, requested, ct))
            decision = ScopeDecision.Forbidden;
        if (await BoardHttp.SendDecisionAsync(HttpContext, decision, ct))
            return;

        if (req.PropertyId is not { } propertyId)
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("propertyId", "Choose a property."));
        var email = req.Email?.Trim();
        if (string.IsNullOrEmpty(email) || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("email", "Enter a valid email address."));

        var companyId = await companyContext.ForCommunityAsync(req.CommunityId, ct) ?? throw CompanyErrors.Forbidden();
        var invitation = await invitations.CreateAsync(new NewInvitation(InvitationKind.Residency, companyId, req.CommunityId,
            propertyId, null, null, email), ResidentsHttp.Actor(User), ct);
        await SendAsync(new MembershipInvitationResponse(invitation.Id, invitation.State.ToString(), invitation.ExpiresAt),
            StatusCodes.Status201Created, ct);
    }
}

/// <summary>GET /communities/{communityId}/residents/{propertyId}/removal-preview?userId=…</summary>
public class RemovalPreviewEndpoint(ApplicationDbContext db, ICommunityScopeResolver scope, ResidentRemovalService removals)
    : EndpointWithoutRequest<RemovalPreview>
{
    public override void Configure()
    {
        Get("/communities/{communityId}/residents/{propertyId}/removal-preview");
        Description(x => x.WithName("ResidentRemovalPreview").WithTags("Community settings"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        var communityId = Route<Guid>("communityId");
        var propertyId = Route<Guid>("propertyId");
        var decision = await scope.DecideAsync(User, communityId, CommunityCapability.ManageResidents, ScopeOperation.Read, ct);
        if (decision == ScopeDecision.Allowed && !await ResidentsHttp.InCommunityAsync(db, communityId, propertyId, ct))
            decision = ScopeDecision.Forbidden;
        if (await BoardHttp.SendDecisionAsync(HttpContext, decision, ct))
            return;

        var userId = Query<string?>("userId", isRequired: false);
        if (string.IsNullOrWhiteSpace(userId))
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("userId", "Choose a resident."));
        await SendOkAsync(await removals.PreviewAsync(propertyId, userId, ct), ct);
    }
}

/// <summary>DELETE /communities/{communityId}/residents/{propertyId}/{userId} — in one transaction (FR-024c).</summary>
public class ResidentRemoveEndpoint(
    ApplicationDbContext db, ICommunityScopeResolver scope, ResidentRemovalService removals,
    Payments.Alerts.OutboxDispatcher dispatcher)
    : EndpointWithoutRequest<RemovalResult>
{
    public override void Configure()
    {
        Delete("/communities/{communityId}/residents/{propertyId}/{userId}");
        Description(x => x.WithName("RemoveResident").WithTags("Community settings").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        var communityId = Route<Guid>("communityId");
        var propertyId = Route<Guid>("propertyId");
        var userId = Route<string>("userId")!;
        var decision = await scope.DecideAsync(User, communityId, CommunityCapability.ManageResidents, ScopeOperation.Write, ct);
        if (decision == ScopeDecision.Allowed && !await ResidentsHttp.InCommunityAsync(db, communityId, propertyId, ct))
            decision = ScopeDecision.Forbidden;
        if (await BoardHttp.SendDecisionAsync(HttpContext, decision, ct))
            return;

        var actorId = ResidentsHttp.Actor(User);
        var result = await CommunityLocks.RunAsync(db, communityId,
            () => removals.RemoveAsync(communityId, propertyId, userId, actorId, ct), ct);
        // The emails were committed with the removal; send them now (the reconcile sweep is the backstop).
        await dispatcher.DispatchPendingAsync(ct);
        await SendOkAsync(result, ct);
    }
}
