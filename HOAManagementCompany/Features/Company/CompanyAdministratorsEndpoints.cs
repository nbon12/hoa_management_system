using System.Net.Mail;
using System.Security.Claims;
using FastEndpoints;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Features.Board.Settings;
using HOAManagementCompany.Features.Invitations;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Company;

// 030 US7 (FR-037, FR-039; contracts/company.md § Company Administrators). Every route here needs
// ManageCompanyAdministrators, which only a live Company Administrator grant holds.

public sealed class CompanyPageQuery
{
    public string? State { get; set; }
    public int? Limit { get; set; }
    public int? Offset { get; set; }
}

public sealed record CompanyAdministratorItem(
    string UserId, string Name, string? Email, DateTimeOffset GrantedAt, string? GrantedBy, string Source);

public sealed class CompanyAdminInviteRequest
{
    public string? Email { get; set; }
}

public sealed record CompanyAdminInviteResponse(Guid Id, string State, DateTimeOffset? ExpiresAt);

public sealed record CompanyAdminRevokeResponse(bool Revoked, int InvitationsRevoked);

internal static class CompanyAdminHttp
{
    public static string Actor(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")!.Value;

    /// <summary>The caller's company when they may manage its administrators, else null (and a 403 is sent).</summary>
    public static async Task<Guid?> AuthorizeAsync(HttpContext ctx, ICommunityScopeResolver scope, ICompanyContext companyContext,
        CancellationToken ct)
    {
        if (await scope.CanAccessCompanyAsync(ctx.User, CompanyCapability.ManageCompanyAdministrators, ct)
            && await companyContext.ForUserAsync(ctx.User, ct) is { } companyId)
            return companyId;
        await BoardHttp.ForbiddenAsync(ctx, ct);
        return null;
    }
}

/// <summary>GET /company/administrators — live grants, by name A–Z.</summary>
public class CompanyAdministratorsListEndpoint(ApplicationDbContext db, ICommunityScopeResolver scope, ICompanyContext companyContext)
    : Endpoint<CompanyPageQuery, PagedResponse<CompanyAdministratorItem>>
{
    public override void Configure()
    {
        Get("/company/administrators");
        Description(x => x.WithName("CompanyAdministrators").WithTags("Company"));
    }

    public override async Task HandleAsync(CompanyPageQuery req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (await CompanyAdminHttp.AuthorizeAsync(HttpContext, scope, companyContext, ct) is not { } companyId)
            return;

        var (limit, offset) = Paging.Normalize(req.Limit, req.Offset);
        var grants = db.CompanyAdministratorGrants.AsNoTracking().Where(g => g.CompanyId == companyId && g.RevokedAt == null);
        var total = await grants.CountAsync(ct);
        var items = await grants
            .OrderBy(g => g.User.FirstName).ThenBy(g => g.User.LastName).ThenBy(g => g.UserId)
            .Skip(offset).Take(limit)
            .Select(g => new CompanyAdministratorItem(
                g.UserId, (g.User.FirstName + " " + g.User.LastName).Trim(), g.User.Email, g.GrantedAt,
                g.Source == CompanyGrantSource.Setup || g.GrantedByUserId == null
                    ? null
                    : db.Users.Where(u => u.Id == g.GrantedByUserId).Select(u => (u.FirstName + " " + u.LastName).Trim()).FirstOrDefault(),
                g.Source.ToString()))
            .ToListAsync(ct);
        await SendOkAsync(new PagedResponse<CompanyAdministratorItem>(items, total, limit, offset), ct);
    }
}

/// <summary>GET /company/invitations — Company Administrator invitations, newest first. Accepted ones are listed as administrators.</summary>
public class CompanyInvitationsListEndpoint(
    ApplicationDbContext db, ICommunityScopeResolver scope, ICompanyContext companyContext, TimeProvider clock)
    : Endpoint<CompanyPageQuery, PagedResponse<InvitationItem>>
{
    public override void Configure()
    {
        Get("/company/invitations");
        Description(x => x.WithName("CompanyInvitations").WithTags("Company"));
    }

    public override async Task HandleAsync(CompanyPageQuery req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (await CompanyAdminHttp.AuthorizeAsync(HttpContext, scope, companyContext, ct) is not { } companyId)
            return;

        InvitationState? state = null;
        if (!string.IsNullOrWhiteSpace(req.State))
        {
            if (!Enum.TryParse<InvitationState>(req.State, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed)
                || int.TryParse(req.State, out _))
                throw CompanyErrors.Validation(new CompanyErrors.FieldError("state", "state must be pending, scheduled or revoked."));
            state = parsed;
        }

        var (limit, offset) = Paging.Normalize(req.Limit, req.Offset);
        var invitations = db.Invitations.AsNoTracking()
            .Where(i => i.Kind == InvitationKind.CompanyAdministrator && i.CompanyId == companyId && i.State != InvitationState.Accepted)
            .Where(i => state == null || i.State == state);
        var total = await invitations.CountAsync(ct);
        var page = await invitations.OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id).Skip(offset).Take(limit).ToListAsync(ct);
        var items = await InvitationItems.MapAsync(db, page, clock.GetUtcNow(), _ => true, ct);
        await SendOkAsync(new PagedResponse<InvitationItem>(items, total, limit, offset), ct);
    }
}

/// <summary>POST /company/administrators/invitations — the response doesn't say whether the email has an account.</summary>
public class CompanyAdministratorsInviteEndpoint(
    ICommunityScopeResolver scope, ICompanyContext companyContext, InvitationService invitations, TimeProvider clock,
    ILogger<CompanyAdministratorsInviteEndpoint> logger)
    : Endpoint<CompanyAdminInviteRequest, CompanyAdminInviteResponse>
{
    public override void Configure()
    {
        Post("/company/administrators/invitations");
        Description(x => x.WithName("InviteCompanyAdministrator").WithTags("Company").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(CompanyAdminInviteRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (await CompanyAdminHttp.AuthorizeAsync(HttpContext, scope, companyContext, ct) is not { } companyId)
            return;

        var email = req.Email?.Trim();
        if (string.IsNullOrEmpty(email) || email.Length > Invitation.MaxEmailLength || !MailAddress.TryCreate(email, out var parsed)
            || parsed.Address != email)
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("email", "Enter a valid email address."));

        var invitation = await invitations.CreateAsync(new NewInvitation(InvitationKind.CompanyAdministrator, companyId, null, null,
            null, null, email), CompanyAdminHttp.Actor(User), ct);
        CompanyLog.CompanyAdminChange(logger, CompanyAdminHttp.Actor(User), companyId, null, invitation.Id, "Invited",
            clock.GetUtcNow());
        await SendAsync(new CompanyAdminInviteResponse(invitation.Id, invitation.State.ToString(), invitation.ExpiresAt),
            StatusCodes.Status201Created, ct);
    }
}

/// <summary>
/// POST /company/administrators/{userId}/revoke — under the company lock, never leaving the company
/// without a live grant. Open invitations the user could only send as an administrator are revoked
/// in the same transaction; ones they can still send as a Community Manager are kept.
/// </summary>
public class CompanyAdministratorsRevokeEndpoint(
    ApplicationDbContext db, ICommunityScopeResolver scope, ICompanyContext companyContext, SenderAuthority senderAuthority,
    TimeProvider clock, ILogger<CompanyAdministratorsRevokeEndpoint> logger)
    : EndpointWithoutRequest<CompanyAdminRevokeResponse>
{
    public override void Configure()
    {
        Post("/company/administrators/{userId}/revoke");
        Description(x => x.WithName("RevokeCompanyAdministrator").WithTags("Company").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (await CompanyAdminHttp.AuthorizeAsync(HttpContext, scope, companyContext, ct) is not { } companyId)
            return;
        var userId = Route<string>("userId")!;
        var actorId = CompanyAdminHttp.Actor(User);

        var now = clock.GetUtcNow();
        var response = await CompanyLocks.RunAsync(db, companyId, async () =>
        {
            // The execution strategy may run this body again after a transient failure; start
            // from the database, not from entities a rolled-back attempt left tracked.
            db.ChangeTracker.Clear();
            var grant = await db.CompanyAdministratorGrants
                            .FirstOrDefaultAsync(g => g.CompanyId == companyId && g.UserId == userId && g.RevokedAt == null, ct)
                        ?? throw new DomainException("NOT_FOUND", "That person isn't a Company Administrator.",
                            StatusCodes.Status404NotFound);
            // The rule first: the loser of two concurrent revokes has lost their own grant by now,
            // and must hear LAST_COMPANY_ADMIN rather than FORBIDDEN (US7-S4).
            if (await db.CompanyAdministratorGrants.CountAsync(g => g.CompanyId == companyId && g.RevokedAt == null, ct) <= 1)
                throw CompanyErrors.LastCompanyAdmin(userId == actorId
                    ? CompanyErrors.LastCompanyAdmin().Message
                    : "This is the only Company Administrator, so the role can't be revoked. Invite another Company Administrator first.");
            if (!await db.CompanyAdministratorGrants.AnyAsync(g => g.CompanyId == companyId && g.UserId == actorId && g.RevokedAt == null, ct))
                throw CompanyErrors.Forbidden();

            grant.RevokedAt = now;
            grant.RevokedByUserId = actorId;
            await db.SaveChangesAsync(ct);

            var revoked = await senderAuthority.RevokeOrphanedAsync(userId, null, actorId, "CompanyAdminRevoked", ct);
            await db.SaveChangesAsync(ct);
            return new CompanyAdminRevokeResponse(true, revoked);
        }, ct);
        CompanyLog.CompanyAdminChange(logger, actorId, companyId, userId, null, "Revoked", now);
        await SendOkAsync(response, ct);
    }
}

/// <summary>
/// POST /company/invitations/{invitationId}/resend and /revoke, with no body: the company routes
/// for Company Administrator invitations. Another company's invitation, or one of another kind,
/// gets the same 403 as no access.
/// </summary>
public abstract class CompanyInvitationActionEndpoint(
    ApplicationDbContext db, ICommunityScopeResolver scope, ICompanyContext companyContext, InvitationService invitations,
    TimeProvider clock)
    : EndpointWithoutRequest<InvitationItem>
{
    protected abstract string Action { get; }

    protected abstract Task<Invitation> ApplyAsync(InvitationService service, string actorId, Guid invitationId, CancellationToken ct);

    public override void Configure()
    {
        Post($"/company/invitations/{{invitationId}}/{Action}");
        Description(x => x.WithName($"CompanyInvitation{Action}").WithTags("Company").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        if (await CompanyAdminHttp.AuthorizeAsync(HttpContext, scope, companyContext, ct) is not { } companyId)
            return;
        var invitationId = Route<Guid>("invitationId");
        if (!await db.Invitations.AnyAsync(i => i.Id == invitationId && i.CompanyId == companyId
                                                && i.Kind == InvitationKind.CompanyAdministrator, ct))
        {
            await BoardHttp.ForbiddenAsync(HttpContext, ct);
            return;
        }

        var invitation = await ApplyAsync(invitations, CompanyAdminHttp.Actor(User), invitationId, ct);
        var item = (await InvitationItems.MapAsync(db, [invitation], clock.GetUtcNow(), _ => true, ct)).Single();
        await SendOkAsync(item, ct);
    }
}

public class CompanyInvitationResendEndpoint(
    ApplicationDbContext db, ICommunityScopeResolver scope, ICompanyContext companyContext, InvitationService invitations,
    TimeProvider clock)
    : CompanyInvitationActionEndpoint(db, scope, companyContext, invitations, clock)
{
    protected override string Action => "resend";

    protected override Task<Invitation> ApplyAsync(InvitationService service, string actorId, Guid invitationId, CancellationToken ct) =>
        service.ResendAsync(actorId, invitationId, ct);
}

public class CompanyInvitationRevokeEndpoint(
    ApplicationDbContext db, ICommunityScopeResolver scope, ICompanyContext companyContext, InvitationService invitations,
    TimeProvider clock)
    : CompanyInvitationActionEndpoint(db, scope, companyContext, invitations, clock)
{
    protected override string Action => "revoke";

    protected override Task<Invitation> ApplyAsync(InvitationService service, string actorId, Guid invitationId, CancellationToken ct) =>
        service.RevokeAsync(actorId, invitationId, ct);
}
