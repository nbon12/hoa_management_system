using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Infrastructure.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HOAManagementCompany.Features.Invitations;

// <!-- REPOWISE:START domain=invitations -->
// The invitation lifecycle shared by membership, residency and Company Administrator invitations
// (030 FR-022, FR-022a, FR-041c; research R6, R12). Every change runs under the community lock (or
// the company lock for Company Administrator invitations) with a row lock on the invitation, so an
// accept and a revoke of the same invitation can't both win. A link token exists in plain text only
// in memory: the row keeps its SHA-256 hash, and the email is sent directly, after the commit, never
// through the outbox. Refusals follow FR-022's order: invalid, expired, sender authority, email.
// The sender's authority is checked again under the lock on create, resend and revoke, so a revoke or
// an archive that committed while the request waited wins.
// <!-- REPOWISE:END -->

/// <summary>A new invitation as the caller asks for it. The kind's handler validates the target.</summary>
public sealed record NewInvitation(
    InvitationKind Kind,
    Guid CompanyId,
    Guid? CommunityId,
    Guid? PropertyId,
    CommunityRole? Role,
    DateOnly? TermEndDate,
    string Email);

/// <summary>A link issued inside a lock and emailed after the commit. The token lives only here.</summary>
public sealed class PendingSend
{
    internal PendingSend(Invitation invitation, string token, InvitationDelivery delivery, bool resent)
    {
        Invitation = invitation;
        Token = token;
        Delivery = delivery;
        Resent = resent;
    }

    public Invitation Invitation { get; }
    internal string Token { get; }
    internal InvitationDelivery Delivery { get; }
    internal bool Resent { get; }
}

public sealed record DeliveryReport(int Sent, int Failed);

/// <summary>The activation fan-out's work: links to email after the commit, and how many were revoked.</summary>
public sealed record ScheduledRelease(IReadOnlyList<PendingSend> Sends, IReadOnlyList<Invitation> Revoked);

public sealed record InvitationPreview(
    string Kind,
    string CommunityName,
    InvitationOfferDto Offer,
    string? SentBy,
    DateTimeOffset ExpiresAt,
    string TimeZoneId,
    string? InvitedEmail,
    bool? HasAccount);

public sealed record InvitationOfferDto(string? Role, DateOnly? TermEndDate, string? PropertyAddress);

public sealed record AcceptOutcome(bool AlreadyHeld);

public sealed class InvitationService(
    ApplicationDbContext db,
    IEnumerable<IInvitationOfferHandler> handlers,
    SenderAuthority senderAuthority,
    IEnumerable<IAlertProvider> alertProviders,
    UserManager<ApplicationUser> userManager,
    AppLinks links,
    IOptions<InvitationOptions> options,
    TimeProvider clock,
    IInvitationLinkObserver linkObserver,
    ILogger<InvitationService> logger)
{
    private const string SendFailed = "The email couldn't be sent.";
    private const string SetupActor = "setup";

    private readonly Dictionary<InvitationKind, IInvitationOfferHandler> _handlers = handlers.ToDictionary(h => h.Kind);
    private InvitationOptions Limits => options.Value;

    // ── Create, resend, revoke ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an invitation and, unless it is a residency in an Onboarding community (Scheduled,
    /// FR-024b), emails it. The caller has already authorized <paramref name="senderUserId"/>; a
    /// null sender is the setup step. An open invitation for the same target refuses with
    /// <c>INVITATION_PENDING</c>, unless it has expired, in which case it is revoked as
    /// <c>Superseded</c> first.
    /// </summary>
    public async Task<Invitation> CreateAsync(NewInvitation request, string? senderUserId, CancellationToken ct)
    {
        var handler = HandlerFor(request.Kind);
        var sends = new List<PendingSend>();
        var actor = senderUserId ?? SetupActor;

        var invitation = await LockAsync(request.Kind, request.CompanyId, request.CommunityId, async () =>
        {
            sends.Clear();
            var now = clock.GetUtcNow();
            var invitation = new Invitation
            {
                Id = Guid.NewGuid(),
                Kind = request.Kind,
                CompanyId = request.CompanyId,
                CommunityId = request.CommunityId,
                PropertyId = request.PropertyId,
                Role = request.Role,
                TermEndDate = request.TermEndDate,
                Email = request.Email.Trim(),
                NormalizedEmail = Invitation.Normalize(request.Email),
                SenderUserId = senderUserId,
                CreatedAt = now,
            };
            await EnsureActorMayAsync(senderUserId, invitation, ct);
            await handler.ValidateCreateAsync(invitation, ct);

            if (await OpenForTargetAsync(invitation, ct) is { } open)
            {
                if (!open.IsExpired(now))
                    throw await DuplicateAsync(open, ct);
                await MarkRevokedAsync(open, now, revokedBy: senderUserId, InvitationRevokeReason.Superseded, actor, cause: null, ct);
                await db.SaveChangesAsync(ct);
            }

            var scheduled = invitation.Kind == InvitationKind.Residency
                && await db.Communities.AnyAsync(c => c.Id == invitation.CommunityId && c.Status == CommunityStatus.Onboarding, ct);
            invitation.State = scheduled ? InvitationState.Scheduled : InvitationState.Pending;
            if (!scheduled)
                await EnforceRecipientCapAsync(invitation.NormalizedEmail, now, ct);

            db.Invitations.Add(invitation);
            if (!scheduled)
                sends.Add(Issue(invitation, now, bypassesCap: false, resent: false));
            await db.SaveChangesAsync(ct);
            await LogAsync(actor, invitation, scheduled ? "Scheduled" : "Created", reason: null, cause: null, now, ct);
            return invitation;
        }, ct);

        await DeliverAsync(sends, ct);
        return invitation;
    }

    /// <summary>
    /// Emails a pending or expired invitation a new link with a new 72 hours, and makes
    /// <paramref name="actorUserId"/> its sender (FR-022). The old link stops working. A null actor
    /// is the setup step resending its own Company Administrator invitation.
    /// </summary>
    public async Task<Invitation> ResendAsync(string? actorUserId, Guid invitationId, CancellationToken ct)
    {
        var target = await TargetAsync(invitationId, ct) ?? throw CompanyErrors.NotFound("The invitation");
        if (actorUserId is null && target.Kind != InvitationKind.CompanyAdministrator)
            throw new InvalidOperationException("Only the setup step resends without a sender, and only Company Administrator invitations.");
        var sends = new List<PendingSend>();

        var invitation = await LockAsync(target.Kind, target.CompanyId, target.CommunityId, async () =>
        {
            sends.Clear();
            var invitation = await LockRowAsync(invitationId, ct);
            await EnsureActorMayAsync(actorUserId, invitation, ct);
            var now = clock.GetUtcNow();
            var outcome = InvitationTransitions.Apply(InvitationTransitions.ViewOf(invitation, now), InvitationAction.Resend);
            if (outcome.RefusalCode is { } code)
                throw await RefusalAsync(code, invitation, ct);

            if (invitation.SentAt is { } sentAt && now - sentAt < TimeSpan.FromSeconds(Limits.ResendCooldownSeconds))
                throw CompanyErrors.RateLimited();
            await EnforceRecipientCapAsync(invitation.NormalizedEmail, now, ct);

            invitation.SenderUserId = actorUserId;
            invitation.ResendCount++;
            sends.Add(Issue(invitation, now, bypassesCap: false, resent: true));
            await db.SaveChangesAsync(ct);
            await LogAsync(actorUserId ?? SetupActor, invitation, "Resent", reason: null, cause: null, now, ct);
            return invitation;
        }, ct);

        await DeliverAsync(sends, ct);
        return invitation;
    }

    /// <summary>Revokes a scheduled, pending or expired invitation (<c>Manual</c>).</summary>
    public async Task<Invitation> RevokeAsync(string actorUserId, Guid invitationId, CancellationToken ct)
    {
        var target = await TargetAsync(invitationId, ct) ?? throw CompanyErrors.NotFound("The invitation");

        return await LockAsync(target.Kind, target.CompanyId, target.CommunityId, async () =>
        {
            var invitation = await LockRowAsync(invitationId, ct);
            await EnsureActorMayAsync(actorUserId, invitation, ct);
            var now = clock.GetUtcNow();
            var outcome = InvitationTransitions.Apply(InvitationTransitions.ViewOf(invitation, now), InvitationAction.Revoke);
            if (outcome.RefusalCode is { } code)
                throw await RefusalAsync(code, invitation, ct);

            await MarkRevokedAsync(invitation, now, actorUserId, InvitationRevokeReason.Manual, actorUserId, cause: null, ct);
            await db.SaveChangesAsync(ct);
            return invitation;
        }, ct);
    }

    // ── Activation fan-out (FR-024b) ────────────────────────────────────────────────────────

    /// <summary>
    /// Inside the activation's community lock and transaction: moves each scheduled invitation
    /// whose sender is still authorized to Pending with a fresh link, and revokes the rest
    /// (<c>SenderLostAuthority</c>). The caller saves, commits, then calls
    /// <see cref="DeliverAsync"/>. These sends bypass the per-recipient cap (FR-041c).
    /// </summary>
    public async Task<ScheduledRelease> ReleaseScheduledAsync(Guid communityId, string actorUserId, string cause,
        CancellationToken ct)
    {
        var scheduled = await db.Invitations
            .FromSqlInterpolated($"""
                SELECT * FROM "Invitations"
                WHERE "CommunityId" = {communityId} AND "State" = 'Scheduled'
                FOR UPDATE
                """)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(ct);

        var now = clock.GetUtcNow();
        var sends = new List<PendingSend>();
        var revoked = new List<Invitation>();
        foreach (var invitation in scheduled)
        {
            if (!await senderAuthority.CouldSendAsync(invitation.SenderUserId, invitation, ct))
            {
                await MarkRevokedAsync(invitation, now, revokedBy: null, InvitationRevokeReason.SenderLostAuthority, actorUserId, cause, ct);
                revoked.Add(invitation);
                continue;
            }
            invitation.State = InvitationState.Pending;
            sends.Add(Issue(invitation, now, bypassesCap: true, resent: false));
            await LogAsync(actorUserId, invitation, "Sent", reason: null, cause, now, ct);
        }
        return new ScheduledRelease(sends, revoked);
    }

    /// <summary>
    /// Inside the archive's community lock and transaction: revokes every scheduled and pending
    /// invitation of the community (<c>CommunityArchived</c>). The caller saves.
    /// </summary>
    public async Task<IReadOnlyList<Invitation>> RevokeForArchiveAsync(Guid communityId, string actorUserId,
        CancellationToken ct)
    {
        var open = await db.Invitations
            .FromSqlInterpolated($"""
                SELECT * FROM "Invitations"
                WHERE "CommunityId" = {communityId} AND "State" IN ('Scheduled', 'Pending')
                FOR UPDATE
                """)
            .ToListAsync(ct);
        var now = clock.GetUtcNow();
        foreach (var invitation in open)
            await MarkRevokedAsync(invitation, now, revokedBy: null, InvitationRevokeReason.CommunityArchived, actorUserId, cause: "CommunityArchived", ct);
        return open;
    }

    /// <summary>
    /// Emails links issued inside a lock, after its commit. A failed send leaves the invitation
    /// Pending with <see cref="Invitation.LastSendError"/>; a later successful send clears it.
    /// </summary>
    public async Task<DeliveryReport> DeliverAsync(IReadOnlyList<PendingSend> sends, CancellationToken ct)
    {
        if (sends.Count == 0)
            return new DeliveryReport(0, 0);

        var email = alertProviders.FirstOrDefault(p => p.Channel == "email");
        int sent = 0, failed = 0;
        foreach (var send in sends)
        {
            var invitation = send.Invitation;
            var link = InvitationTokens.Link(links, send.Token);
            var normalizedUpper = invitation.NormalizedEmail.ToUpperInvariant();
            var hasAccount = await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedUpper, ct);
            var message = InvitationEmailRenderer.Render(await DescribeAsync(invitation, ct), invitation.Email, link,
                invitation.ExpiresAt!.Value, hasAccount, send.Resent);

            var succeeded = false;
            try
            {
                succeeded = email is { IsConfigured: true } && (await email.SendAsync(message, ct)).Success;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The body holds the link, so only the type is logged.
                logger.LogWarning("InvitationSendFailed: Invitation={InvitationId} Error={ErrorType}", invitation.Id, ex.GetType().Name);
            }
            linkObserver.LinkIssued(invitation.Email, link);

            send.Delivery.Succeeded = succeeded;
            invitation.LastSendError = succeeded ? null : SendFailed;
            if (succeeded) sent++;
            else
            {
                failed++;
                logger.LogWarning("InvitationSendFailed: Invitation={InvitationId} Configured={Configured}",
                    invitation.Id, email?.IsConfigured ?? false);
            }
        }
        await db.SaveChangesAsync(ct);
        return new DeliveryReport(sent, failed);
    }

    // ── Acceptance (preview, accept, register) ──────────────────────────────────────────────

    /// <summary>
    /// What a valid, unexpired link offers. <see cref="InvitationPreview.InvitedEmail"/> and
    /// <see cref="InvitationPreview.HasAccount"/> are filled only for an anonymous caller (FR-022a),
    /// so a different signed-in account never receives the invited email.
    /// </summary>
    public async Task<InvitationPreview> PreviewAsync(string token, bool anonymous, CancellationToken ct)
    {
        var hash = InvitationTokens.Hash(token ?? string.Empty);
        var invitation = await db.Invitations.AsNoTracking().FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
        var now = clock.GetUtcNow();
        if (Validity(invitation, now) is { } code)
            throw Refused(code, invitation, userId: null, now);

        var description = await DescribeAsync(invitation!, ct);
        var hasAccount = anonymous
            ? await db.Users.AnyAsync(u => u.NormalizedEmail == invitation!.NormalizedEmail.ToUpperInvariant(), ct)
            : (bool?)null;
        return new InvitationPreview(
            invitation!.Kind.ToString(),
            description.CommunityName ?? description.CompanyName,
            new InvitationOfferDto(invitation.Role?.ToString(), invitation.TermEndDate, description.PropertyAddress),
            description.SentBy,
            invitation.ExpiresAt!.Value,
            description.TimeZoneId,
            anonymous ? invitation.Email : null,
            hasAccount);
    }

    /// <summary>Applies the link's offer to the signed-in caller and marks the invitation Accepted.</summary>
    public async Task<AcceptOutcome> AcceptAsync(string token, string userId, CancellationToken ct)
    {
        var (_, alreadyHeld) = await AcceptCoreAsync(token, userId, register: null, ct);
        return new AcceptOutcome(alreadyHeld);
    }

    /// <summary>
    /// FR-022a: creates the account for the invited email (email confirmed, no claim code), applies
    /// the offer and marks the invitation Accepted, in one transaction. An existing account for
    /// that email refuses with the generic <c>REGISTRATION_FAILED</c> and changes nothing.
    /// </summary>
    public async Task<ApplicationUser> RegisterAsync(string token, string firstName, string lastName, string password,
        CancellationToken ct)
    {
        var (user, _) = await AcceptCoreAsync(token, userId: null, new Registration(firstName, lastName, password), ct);
        return user;
    }

    private sealed record Registration(string FirstName, string LastName, string Password);

    private sealed record AcceptResult(string? RefusalCode, Invitation? Invitation, ApplicationUser? User, bool AlreadyHeld,
        string? ArchivedCommunityName = null);

    private async Task<(ApplicationUser User, bool AlreadyHeld)> AcceptCoreAsync(
        string token, string? userId, Registration? register, CancellationToken ct)
    {
        var hash = InvitationTokens.Hash(token ?? string.Empty);
        var target = await db.Invitations.AsNoTracking()
            .Where(i => i.TokenHash == hash)
            .Select(i => new LockTarget(i.Id, i.Kind, i.CompanyId, i.CommunityId))
            .FirstOrDefaultAsync(ct);
        if (target is null)
            throw Refused(InvitationTransitions.Invalid, invitation: null, userId, clock.GetUtcNow());

        var result = await LockAsync(target.Kind, target.CompanyId, target.CommunityId, async () =>
        {
            var invitation = await LockRowAsync(target.Id, ct);
            var now = clock.GetUtcNow();

            // A resend may have replaced the link while this request waited for the lock.
            if (invitation.TokenHash != hash)
                return new AcceptResult(InvitationTransitions.Invalid, invitation, null, false);
            if (Validity(invitation, now) is { } code)
                return new AcceptResult(code, invitation, null, false);

            // Kept for races; archiving revokes every open invitation under the same lock.
            if (invitation.CommunityId is { } communityId
                && await db.Communities.Where(c => c.Id == communityId && c.Status == CommunityStatus.Archived)
                    .Select(c => c.CommunityName).FirstOrDefaultAsync(ct) is { } archivedName)
                return new AcceptResult("COMMUNITY_ARCHIVED", invitation, null, false, archivedName);

            if (!await senderAuthority.CouldSendAsync(invitation.SenderUserId, invitation, ct))
            {
                await MarkRevokedAsync(invitation, now, revokedBy: null, InvitationRevokeReason.SenderLostAuthority,
                    invitation.SenderUserId ?? SetupActor, cause: "InvitationAccept", ct);
                await db.SaveChangesAsync(ct);
                return new AcceptResult(InvitationTransitions.Invalid, invitation, null, false);
            }

            ApplicationUser user;
            if (register is null)
            {
                user = await db.Users.FirstAsync(u => u.Id == userId, ct);
                if (Invitation.Normalize(user.Email ?? string.Empty) != invitation.NormalizedEmail)
                    return new AcceptResult("INVITATION_EMAIL_MISMATCH", invitation, user, false);
            }
            else
            {
                if (await db.Users.AnyAsync(u => u.NormalizedEmail == invitation.NormalizedEmail.ToUpperInvariant(), ct))
                    return new AcceptResult("REGISTRATION_FAILED", invitation, null, false);
                try
                {
                    user = await CreateUserAsync(invitation, register);
                }
                catch (DomainException ex)
                {
                    // A refused sign-up is a failed acceptance too (FR-024e).
                    CompanyLog.InvitationAcceptFailed(logger, invitation.Id, invitation.CommunityId, invitation.PropertyId,
                        null, ex.Code, now);
                    throw;
                }
            }

            var alreadyHeld = await HandlerFor(invitation.Kind).ApplyAsync(invitation, user.Id, ct);
            invitation.State = InvitationState.Accepted;
            invitation.AcceptedAt = now;
            invitation.AcceptedByUserId = user.Id;
            await db.SaveChangesAsync(ct);
            await LogAsync(user.Id, invitation, "Accepted", reason: alreadyHeld ? "AlreadyHeld" : null, cause: null, now, ct);
            return new AcceptResult(null, invitation, user, alreadyHeld);
        }, ct);

        if (result.RefusalCode is { } refusal)
            throw Refused(refusal, result.Invitation, userId ?? result.User?.Id, clock.GetUtcNow(), result.ArchivedCommunityName);
        return (result.User!, result.AlreadyHeld);
    }

    private async Task<ApplicationUser> CreateUserAsync(Invitation invitation, Registration register)
    {
        var user = new ApplicationUser
        {
            Email = invitation.Email,
            UserName = invitation.Email,
            FirstName = register.FirstName.Trim(),
            LastName = register.LastName.Trim(),
            // The link proves the email address (FR-022a).
            EmailConfirmed = true,
            LockoutEnabled = true,
        };
        var created = await userManager.CreateAsync(user, register.Password);
        if (!created.Succeeded)
        {
            var passwordErrors = created.Errors.Where(e => e.Code.StartsWith("Password", StringComparison.Ordinal)).ToList();
            if (passwordErrors.Count == 0)
                throw CompanyErrors.RegistrationFailed();
            throw CompanyErrors.Validation(passwordErrors
                .Select(e => new CompanyErrors.FieldError("password", e.Description)).ToArray());
        }
        return user;
    }

    // ── Shared pieces ───────────────────────────────────────────────────────────────────────

    /// <summary>What an invitation offers, as previews and emails show it.</summary>
    public async Task<InvitationDescription> DescribeAsync(Invitation invitation, CancellationToken ct)
    {
        var company = await db.ManagementCompanies.AsNoTracking()
            .Where(c => c.Id == invitation.CompanyId)
            .Select(c => new { c.Name, TimeZoneId = c.DefaultSettings != null ? c.DefaultSettings.TimeZoneId : null })
            .FirstAsync(ct);
        var community = invitation.CommunityId is { } communityId
            ? await db.Communities.AsNoTracking().Where(c => c.Id == communityId).Select(c => new
            {
                c.CommunityName,
                TimeZoneId = db.CommunityArcSettings.Where(s => s.CommunityId == c.Id).Select(s => s.TimeZoneId).FirstOrDefault(),
            }).FirstAsync(ct)
            : null;
        var address = invitation.PropertyId is { } propertyId
            ? await db.Properties.AsNoTracking().Where(p => p.Id == propertyId).Select(p => p.Address).FirstAsync(ct)
            : null;
        var sentBy = invitation.SenderUserId is { } senderId
            ? await db.Users.AsNoTracking().Where(u => u.Id == senderId).Select(u => u.FirstName + " " + u.LastName).FirstAsync(ct)
            : null;

        var timeZoneId = (invitation.Kind == InvitationKind.CompanyAdministrator ? company.TimeZoneId : community?.TimeZoneId)
            ?? CommunityArcSettings.DefaultTimeZoneId;
        return new InvitationDescription(invitation.Kind, company.Name, community?.CommunityName, invitation.Role,
            invitation.TermEndDate, address, sentBy?.Trim(), timeZoneId);
    }

    private IInvitationOfferHandler HandlerFor(InvitationKind kind) =>
        _handlers.TryGetValue(kind, out var handler)
            ? handler
            : throw new InvalidOperationException($"No invitation offer handler is registered for {kind}.");

    private sealed record LockTarget(Guid Id, InvitationKind Kind, Guid CompanyId, Guid? CommunityId);

    private Task<LockTarget?> TargetAsync(Guid invitationId, CancellationToken ct) =>
        db.Invitations.AsNoTracking()
            .Where(i => i.Id == invitationId)
            .Select(i => new LockTarget(i.Id, i.Kind, i.CompanyId, i.CommunityId))
            .FirstOrDefaultAsync(ct);

    // Company Administrator invitations serialize per company; the rest per community (R5).
    // The callers authorized the actor before taking the lock; a revoke or archive that committed
    // while this request waited for it must still win (US7-S2, FR-012). The setup step has no actor.
    private async Task EnsureActorMayAsync(string? actorUserId, Invitation invitation, CancellationToken ct)
    {
        if (actorUserId is not null
            && BoardHttp.RefusalFor(await senderAuthority.DecideAsync(actorUserId, invitation, ct)) is { } refusal)
            throw refusal;
    }

    private Task<T> LockAsync<T>(InvitationKind kind, Guid companyId, Guid? communityId, Func<Task<T>> body, CancellationToken ct) =>
        kind == InvitationKind.CompanyAdministrator
            ? CompanyLocks.RunAsync(db, companyId, body, ct)
            : CommunityLocks.RunAsync(db, communityId!.Value, body, ct);

    private async Task<Invitation> LockRowAsync(Guid invitationId, CancellationToken ct)
    {
        // Tracked rows keep their old values on a re-query, so start from a fresh one.
        if (db.ChangeTracker.Entries<Invitation>().FirstOrDefault(e => e.Entity.Id == invitationId) is { } tracked)
            tracked.State = EntityState.Detached;
        return await db.Invitations
            .FromSqlInterpolated($"""SELECT * FROM "Invitations" WHERE "Id" = {invitationId} FOR UPDATE""")
            .FirstAsync(ct);
    }

    private Task<Invitation?> OpenForTargetAsync(Invitation invitation, CancellationToken ct)
    {
        var open = db.Invitations.Where(i =>
            i.Kind == invitation.Kind
            && i.NormalizedEmail == invitation.NormalizedEmail
            && (i.State == InvitationState.Scheduled || i.State == InvitationState.Pending));
        open = invitation.Kind switch
        {
            InvitationKind.Membership => open.Where(i => i.CommunityId == invitation.CommunityId && i.Role == invitation.Role),
            InvitationKind.Residency => open.Where(i => i.PropertyId == invitation.PropertyId),
            _ => open.Where(i => i.CompanyId == invitation.CompanyId),
        };
        return open.FirstOrDefaultAsync(ct);
    }

    private async Task EnforceRecipientCapAsync(string normalizedEmail, DateTimeOffset now, CancellationToken ct)
    {
        var since = now.AddHours(-24);
        var recent = await db.InvitationDeliveries.CountAsync(d =>
            d.NormalizedEmail == normalizedEmail && !d.BypassesRecipientCap && d.AttemptedAt > since, ct);
        if (recent >= Limits.MaxEmailsPerRecipientPer24h)
            throw CompanyErrors.RateLimited();
    }

    private PendingSend Issue(Invitation invitation, DateTimeOffset now, bool bypassesCap, bool resent)
    {
        var token = InvitationTokens.New();
        invitation.TokenHash = InvitationTokens.Hash(token);
        invitation.SentAt = now;
        invitation.ExpiresAt = now.AddHours(Limits.ExpiryHours);
        var delivery = new InvitationDelivery
        {
            Id = Guid.NewGuid(),
            InvitationId = invitation.Id,
            NormalizedEmail = invitation.NormalizedEmail,
            AttemptedAt = now,
            BypassesRecipientCap = bypassesCap,
        };
        db.InvitationDeliveries.Add(delivery);
        return new PendingSend(invitation, token, delivery, resent);
    }

    private async Task MarkRevokedAsync(Invitation invitation, DateTimeOffset now, string? revokedBy, InvitationRevokeReason reason,
        string actor, string? cause, CancellationToken ct)
    {
        invitation.State = InvitationState.Revoked;
        invitation.RevokedAt = now;
        invitation.RevokedByUserId = revokedBy;
        invitation.RevokeReason = reason;
        await LogAsync(actor, invitation, "Revoked", reason.ToString(), cause, now, ct);
    }

    // FR-022 steps 1 and 2: unknown, used, revoked or past its term → invalid; then expired.
    private string? Validity(Invitation? invitation, DateTimeOffset now)
    {
        if (invitation is null || invitation.State != InvitationState.Pending)
            return InvitationTransitions.Invalid;
        if (invitation.Kind == InvitationKind.Membership && invitation.TermEndDate is { } end
            && end < DateOnly.FromDateTime(now.UtcDateTime))
            return InvitationTransitions.Invalid;
        return invitation.IsExpired(now) ? InvitationTransitions.Expired : null;
    }

    private DomainException Refused(string code, Invitation? invitation, string? userId, DateTimeOffset now,
        string? archivedCommunityName = null)
    {
        CompanyLog.InvitationAcceptFailed(logger, invitation?.Id, invitation?.CommunityId, invitation?.PropertyId,
            userId, code, now);
        return code switch
        {
            InvitationTransitions.Expired => CompanyErrors.InvitationExpired(),
            "INVITATION_EMAIL_MISMATCH" => CompanyErrors.InvitationEmailMismatch(),
            "REGISTRATION_FAILED" => CompanyErrors.RegistrationFailed(),
            "COMMUNITY_ARCHIVED" => CompanyErrors.CommunityArchived(archivedCommunityName!),
            _ => CompanyErrors.InvitationInvalid(),
        };
    }

    // A refused resend or revoke tells the caller the invitation's final state (spec Edge Cases).
    private async Task<DomainException> RefusalAsync(string code, Invitation invitation, CancellationToken ct)
    {
        if (code == InvitationTransitions.NotActive)
        {
            var name = await db.Communities.Where(c => c.Id == invitation.CommunityId).Select(c => c.CommunityName).FirstAsync(ct);
            return CompanyErrors.CommunityNotActive($"This invitation is sent when {name} is marked Active.");
        }
        return new DomainException(InvitationTransitions.Invalid,
            invitation.State == InvitationState.Accepted
                ? "This invitation was already accepted."
                : "This invitation was already revoked.",
            StatusCodes.Status410Gone);
    }

    private async Task<DomainException> DuplicateAsync(Invitation open, CancellationToken ct)
    {
        var description = await DescribeAsync(open, ct);
        var message = open.Kind switch
        {
            InvitationKind.Membership =>
                $"{open.Email} already has a pending {InvitationEmailRenderer.RoleLabel(open.Role)} invitation for {description.CommunityName}. Resend it instead?",
            InvitationKind.Residency when open.State == InvitationState.Scheduled =>
                $"{open.Email} already has a scheduled invitation to {description.PropertyAddress}. It will be sent when {description.CommunityName} is marked Active.",
            InvitationKind.Residency =>
                $"{open.Email} already has a pending invitation to {description.PropertyAddress}. Resend it instead?",
            _ => $"{open.Email} already has a pending Company Administrator invitation. Resend it instead?",
        };
        return CompanyErrors.InvitationPending(open.Id, message);
    }

    private async Task LogAsync(string actor, Invitation invitation, string change, string? reason, string? cause,
        DateTimeOffset now, CancellationToken ct) =>
        CompanyLog.InvitationChange(logger, actor, invitation.CommunityId, invitation.PropertyId, invitation.Id,
            await InviteeUserIdAsync(db, invitation, ct), invitation.Kind.ToString(), change, reason, cause, now);

    /// <summary>FR-023: the invitee's user ID when an account has the invited email, else null.</summary>
    internal static Task<string?> InviteeUserIdAsync(ApplicationDbContext db, Invitation invitation, CancellationToken ct)
    {
        var normalized = invitation.NormalizedEmail.ToUpperInvariant();
        return db.Users.Where(u => u.NormalizedEmail == normalized).Select(u => u.Id).FirstOrDefaultAsync(ct);
    }
}

/// <summary>Told about each link the service emails (030 T054). Only the dev vault listens.</summary>
public interface IInvitationLinkObserver
{
    void LinkIssued(string email, string link);
}

public sealed class NoInvitationLinkObserver : IInvitationLinkObserver
{
    public void LinkIssued(string email, string link) { }
}
