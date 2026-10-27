using System.Security.Claims;
using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Features.Invitations;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Settings;

public sealed class CommunityInvitationsQuery
{
    public Guid CommunityId { get; set; }
    public string? Kind { get; set; }
    public string? State { get; set; }
    public int? Limit { get; set; }
    public int? Offset { get; set; }
}

public sealed record InvitationItem(
    Guid Id, string Kind, string Email, string? Role, DateOnly? TermEndDate, Guid? PropertyId, string State,
    string EffectiveState, string? RevokeReason, string? SentBy, DateTimeOffset? SentAt, DateTimeOffset? ExpiresAt,
    string? LastSendError, bool CanResend, bool CanRevoke, string? ResendBlockedReason);

public sealed record InvitationListResponse(
    IReadOnlyList<InvitationItem> Items, int Total, int Limit, int Offset, string TimeZoneId);

/// <summary>Maps invitations to list rows with what the caller may do (contracts/community-settings.md § Invitations).</summary>
public static class InvitationItems
{
    public static async Task<List<InvitationItem>> MapAsync(ApplicationDbContext db, IReadOnlyList<Invitation> invitations,
        DateTimeOffset now, Func<Invitation, bool> mayWrite, CancellationToken ct)
    {
        var senderIds = invitations.Select(i => i.SenderUserId).OfType<string>().Distinct().ToList();
        var senders = await db.Users.AsNoTracking().Where(u => senderIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim(), ct);
        return invitations.Select(i =>
        {
            var open = i.IsOpen;
            var write = open && mayWrite(i);
            return new InvitationItem(i.Id, i.Kind.ToString(), i.Email, i.Role?.ToString(), i.TermEndDate, i.PropertyId,
                i.State.ToString(), i.EffectiveState(now), i.RevokeReason?.ToString(),
                i.SenderUserId is { } s && senders.TryGetValue(s, out var name) ? name : null,
                i.SentAt, i.ExpiresAt, i.LastSendError,
                CanResend: write && i.State == InvitationState.Pending,
                CanRevoke: write,
                ResendBlockedReason: i.State == InvitationState.Scheduled ? "COMMUNITY_NOT_ACTIVE" : null);
        }).ToList();
    }

    public static async Task<string> TimeZoneAsync(ApplicationDbContext db, Guid communityId, CancellationToken ct) =>
        await db.CommunityArcSettings.Where(s => s.CommunityId == communityId).Select(s => s.TimeZoneId).FirstOrDefaultAsync(ct)
        ?? CommunityArcSettings.DefaultTimeZoneId;

    public static CommunityCapability ListCapability(InvitationKind kind) =>
        kind == InvitationKind.Residency ? CommunityCapability.ManageResidents : CommunityCapability.ManageMemberships;
}

/// <summary>
/// GET /communities/{communityId}/invitations — membership invitations by default; residency
/// with <c>kind=residency</c>. Newest first. Accepted invitations are left out: the person is
/// listed as a member or resident instead. Readable while archived by Company Administrators.
/// </summary>
public class CommunityInvitationsListEndpoint(ApplicationDbContext db, ICommunityScopeResolver scope, TimeProvider clock)
    : Endpoint<CommunityInvitationsQuery, InvitationListResponse>
{
    public override void Configure()
    {
        Get("/communities/{communityId}/invitations");
        Description(x => x.WithName("CommunityInvitations").WithTags("Community settings"));
    }

    public override async Task HandleAsync(CommunityInvitationsQuery req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        var kind = req.Kind?.ToLowerInvariant() switch
        {
            null or "" or "membership" => InvitationKind.Membership,
            "residency" => InvitationKind.Residency,
            _ => throw CompanyErrors.Validation(new CompanyErrors.FieldError("kind", "kind must be membership or residency.")),
        };
        if (await BoardHttp.SendDecisionAsync(HttpContext,
                await scope.DecideAsync(User, req.CommunityId, InvitationItems.ListCapability(kind), ScopeOperation.Read, ct), ct))
            return;

        var now = clock.GetUtcNow();
        var query = db.Invitations.AsNoTracking().Where(i => i.CommunityId == req.CommunityId && i.Kind == kind);
        query = req.State?.ToLowerInvariant() switch
        {
            null or "" => query.Where(i => i.State != InvitationState.Accepted),
            "scheduled" => query.Where(i => i.State == InvitationState.Scheduled),
            "pending" => query.Where(i => i.State == InvitationState.Pending && i.ExpiresAt > now),
            "expired" => query.Where(i => i.State == InvitationState.Pending && i.ExpiresAt <= now),
            "revoked" => query.Where(i => i.State == InvitationState.Revoked),
            _ => throw CompanyErrors.Validation(new CompanyErrors.FieldError("state",
                "state must be scheduled, pending, expired or revoked.")),
        };

        var managersMayWrite = await scope.DecideAsync(User, req.CommunityId, CommunityCapability.ManageCommunityManagers,
            ScopeOperation.Write, ct) == ScopeDecision.Allowed;
        var othersMayWrite = await scope.DecideAsync(User, req.CommunityId, InvitationItems.ListCapability(kind),
            ScopeOperation.Write, ct) == ScopeDecision.Allowed;

        var (limit, offset) = Paging.Normalize(req.Limit, req.Offset);
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id).Skip(offset).Take(limit).ToListAsync(ct);
        var items = await InvitationItems.MapAsync(db, page, now,
            i => i.Role == CommunityRole.CommunityManager ? managersMayWrite : othersMayWrite, ct);
        await SendOkAsync(new InvitationListResponse(items, total, limit, offset,
            await InvitationItems.TimeZoneAsync(db, req.CommunityId, ct)), ct);
    }
}

/// <summary>
/// POST /communities/{communityId}/invitations/{invitationId}/resend and /revoke, with no body.
/// Authorized by the capability that sends the invitation's kind (and role), as a write. An
/// invitation of another community gets the same 403 as a community the caller can't reach.
/// </summary>
public abstract class CommunityInvitationActionEndpoint(
    ApplicationDbContext db, ICommunityScopeResolver scope, InvitationService invitations, TimeProvider clock)
    : EndpointWithoutRequest<InvitationItem>
{
    protected abstract string Action { get; }

    protected abstract Task<Invitation> ApplyAsync(InvitationService service, string actorId, Guid invitationId, CancellationToken ct);

    public override void Configure()
    {
        Post($"/communities/{{communityId}}/invitations/{{invitationId}}/{Action}");
        Description(x => x.WithName($"CommunityInvitation{Action}").WithTags("Community settings").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        var communityId = Route<Guid>("communityId");
        var invitationId = Route<Guid>("invitationId");
        var target = await db.Invitations.AsNoTracking()
            .Where(i => i.Id == invitationId && i.CommunityId == communityId)
            .Select(i => new { i.Kind, i.Role })
            .FirstOrDefaultAsync(ct);
        var decision = target is null
            ? ScopeDecision.Forbidden
            : await scope.DecideAsync(User, communityId, SenderAuthority.CapabilityFor(target.Kind, target.Role),
                ScopeOperation.Write, ct);
        if (await BoardHttp.SendDecisionAsync(HttpContext, decision, ct))
            return;

        var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value;
        var invitation = await ApplyAsync(invitations, actorId, invitationId, ct);
        var item = (await InvitationItems.MapAsync(db, [invitation], clock.GetUtcNow(), _ => true, ct)).Single();
        await SendOkAsync(item, ct);
    }
}

public class CommunityInvitationResendEndpoint(
    ApplicationDbContext db, ICommunityScopeResolver scope, InvitationService invitations, TimeProvider clock)
    : CommunityInvitationActionEndpoint(db, scope, invitations, clock)
{
    protected override string Action => "resend";

    protected override Task<Invitation> ApplyAsync(InvitationService service, string actorId, Guid invitationId, CancellationToken ct) =>
        service.ResendAsync(actorId, invitationId, ct);
}

public class CommunityInvitationRevokeEndpoint(
    ApplicationDbContext db, ICommunityScopeResolver scope, InvitationService invitations, TimeProvider clock)
    : CommunityInvitationActionEndpoint(db, scope, invitations, clock)
{
    protected override string Action => "revoke";

    protected override Task<Invitation> ApplyAsync(InvitationService service, string actorId, Guid invitationId, CancellationToken ct) =>
        service.RevokeAsync(actorId, invitationId, ct);
}
