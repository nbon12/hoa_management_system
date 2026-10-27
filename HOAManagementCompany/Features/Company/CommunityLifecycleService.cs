using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Features.Board.Settings;
using HOAManagementCompany.Features.Invitations;
using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Features.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Company;

public sealed record ActivateResult(string Status, int InvitationsSent, int InvitationsFailedToSend, int InvitationsRevoked);

public sealed record ArchiveResult(string Status, int AutopayCancelled, int ResidentsEmailed, int InvitationsRevoked);

public sealed record RestoreResult(string Status, string Reason);

public sealed record LifecycleRefusal(string Code, string Message, IReadOnlyList<CompanyErrors.OpenWorkItem>? Items);

public sealed record LifecyclePreview(string Action, bool Allowed, LifecycleRefusal? Refusal, IReadOnlyDictionary<string, object?> Consequences);

public sealed record RevokedInvitationLine(Guid Id, string Email, string Kind, string? Role);

public sealed record MembershipLine(Guid MembershipId, string UserId, string Name, string Role, DateOnly? EndDate);

public sealed record ScheduledInvitationLine(Guid Id, string Email, string? PropertyAddress, DateTimeOffset? ExpiresAtIfSentNow);

// <!-- REPOWISE:START domain=company -->
// The community lifecycle (030 FR-010–FR-016; research R4): Onboarding → Active → Archived and
// restore, plus the parent link. Each change runs under the community lock in one transaction, and
// each has a preview that says what would happen without changing anything (brief 04 D4).
// Activation sends the scheduled resident invitations after the commit. Archiving refuses while
// there is open work, then revokes open invitations, cancels autopay and emails every linked
// resident once. Restore returns to Active only if the community was live before and still has an
// open-ended manager. Every caused change is logged with the action's actor and a cause (FR-040).
// <!-- REPOWISE:END -->
public sealed class CommunityLifecycleService(
    ApplicationDbContext db,
    InvitationService invitations,
    SenderAuthority senderAuthority,
    MembershipService memberships,
    CompanyEmailRenderer emails,
    OutboxDispatcher dispatcher,
    TimeProvider clock,
    ILogger<CommunityLifecycleService> logger)
{
    private const int DispatchBatches = 10;

    // ── Activate ────────────────────────────────────────────────────────────────────────────

    public async Task<ActivateResult> ActivateAsync(Guid communityId, string actorId, CancellationToken ct)
    {
        var release = await CommunityLocks.RunAsync(db, communityId, async () =>
        {
            var community = await db.Communities.FirstAsync(c => c.Id == communityId, ct);
            if (await ActivateRefusalAsync(community, ct) is { } refusal)
                throw refusal;

            var now = clock.GetUtcNow();
            // The scheduled invitations are released while the community still reads as Onboarding,
            // so each sender's authority is checked as it stood when they sent it.
            var released = await invitations.ReleaseScheduledAsync(communityId, actorId, "CommunityActivated", ct);
            community.Status = CommunityStatus.Active;
            community.ActivatedAt = now;
            community.ActivatedByUserId = actorId;
            await db.SaveChangesAsync(ct);
            CompanyLog.CommunityLifecycle(logger, actorId, communityId, "Activated",
                nameof(CommunityStatus.Onboarding), nameof(CommunityStatus.Active), now);
            return released;
        }, ct);

        var report = await invitations.DeliverAsync(release.Sends, ct);
        return new ActivateResult(nameof(CommunityStatus.Active), report.Sent, report.Failed, release.Revoked.Count);
    }

    // ── Archive ─────────────────────────────────────────────────────────────────────────────

    public async Task<ArchiveResult> ArchiveAsync(Guid communityId, string actorId, CancellationToken ct)
    {
        var result = await CommunityLocks.RunAsync(db, communityId, async () =>
        {
            var community = await db.Communities.Include(c => c.ManagementCompany).FirstAsync(c => c.Id == communityId, ct);
            if (await ArchiveRefusalAsync(community, ct) is { } refusal)
                throw refusal;

            var now = clock.GetUtcNow();
            var from = community.Status;
            community.Status = CommunityStatus.Archived;
            community.ArchivedAt = now;
            community.ArchivedByUserId = actorId;

            var revoked = await invitations.RevokeForArchiveAsync(communityId, actorId, ct);

            // FR-013: every active schedule in the community stops, with no Stripe call (R9).
            var schedules = await db.RecurringPayments.Include(r => r.Authorizations)
                .Where(r => r.Status == "active" && r.Property.CommunityId == communityId)
                .ToListAsync(ct);
            foreach (var schedule in schedules)
            {
                PaymentService.Cancel(schedule, RecurringCancellationReasons.CommunityArchived, now);
                CompanyLog.AutopayCancelled(logger, actorId, schedule.PropertyId, schedule.Id,
                    RecurringCancellationReasons.CommunityArchived, "CommunityArchived", now);
            }
            var cancelled = schedules.Select(s => s.PropertyId).ToHashSet();

            // One email per linked resident, naming each of their homes here.
            var links = await db.UserProperties.AsNoTracking()
                .Where(up => up.Property.CommunityId == communityId)
                .Select(up => new { up.UserId, up.User.Email, up.PropertyId, up.Property.Address })
                .ToListAsync(ct);
            var emailed = 0;
            foreach (var resident in links.Where(l => !string.IsNullOrEmpty(l.Email)).GroupBy(l => l.UserId))
            {
                var homes = resident.OrderBy(l => l.Address)
                    .Select(l => new CompanyEmailRenderer.PropertyLine(l.Address, cancelled.Contains(l.PropertyId)))
                    .ToList();
                db.OutboxMessages.Add(emails.CommunityArchived(resident.Key, resident.First().Email!, communityId,
                    community.CommunityName, community.ManagementCompany.Name, now, homes));
                emailed++;
            }

            await db.SaveChangesAsync(ct);
            CompanyLog.CommunityLifecycle(logger, actorId, communityId, "Archived", from.ToString(),
                nameof(CommunityStatus.Archived), now);
            return new ArchiveResult(nameof(CommunityStatus.Archived), schedules.Count, emailed, revoked.Count);
        }, ct);

        // Dispatched inline, in at most 10 batches of 100; the reconcile sweep is the backstop.
        for (var batch = 0; batch < DispatchBatches
                            && await db.OutboxMessages.AnyAsync(m => m.Status == OutboxStatus.Pending, ct); batch++)
            await dispatcher.DispatchPendingAsync(ct);
        return result;
    }

    // ── Restore ─────────────────────────────────────────────────────────────────────────────

    public Task<RestoreResult> RestoreAsync(Guid communityId, string actorId, CancellationToken ct) =>
        CommunityLocks.RunAsync(db, communityId, async () =>
        {
            var community = await db.Communities.FirstAsync(c => c.Id == communityId, ct);
            if (await RestoreRefusalAsync(community, ct) is { } refusal)
                throw refusal;

            var (to, reason) = await RestoreTargetAsync(community, ct);
            var now = clock.GetUtcNow();
            community.Status = to;
            community.RestoredAt = now;
            community.RestoredByUserId = actorId;
            await db.SaveChangesAsync(ct);
            CompanyLog.CommunityLifecycle(logger, actorId, communityId, "Restored", nameof(CommunityStatus.Archived),
                to.ToString(), now);
            return new RestoreResult(to.ToString(), reason);
        }, ct);

    // ── Parent ──────────────────────────────────────────────────────────────────────────────

    /// <summary>FR-016: set or clear the parent. Company Administrators only (the caller checks).</summary>
    public Task SetParentAsync(Guid communityId, Guid? parentId, string actorId, CancellationToken ct) =>
        CommunityLocks.RunAsync(db, communityId, async () =>
        {
            var community = await db.Communities.FirstAsync(c => c.Id == communityId, ct);
            if (community.Status == CommunityStatus.Archived)
                throw CompanyErrors.CommunityArchived(community.CommunityName);
            if (community.ParentCommunityId == parentId)
                return 0;

            if (parentId is { } newParentId)
            {
                var parent = await db.Communities.AsNoTracking()
                    .Where(c => c.Id == newParentId && c.ManagementCompanyId == community.ManagementCompanyId)
                    .Select(c => new { c.CommunityName, c.Status })
                    .FirstOrDefaultAsync(ct)
                    ?? throw CompanyErrors.Validation(new CompanyErrors.FieldError("parentCommunityId",
                        "Choose a community of this company as the parent."));
                if (newParentId == communityId || await IsDescendantAsync(newParentId, communityId, ct))
                    throw CompanyErrors.ParentCycle(community.CommunityName, parent.CommunityName);
                if (parent.Status == CommunityStatus.Archived)
                    throw CompanyErrors.ParentArchived(parent.CommunityName);
            }

            var from = community.ParentCommunityId;
            community.ParentCommunityId = parentId;
            await db.SaveChangesAsync(ct);
            CompanyLog.CommunityLifecycle(logger, actorId, communityId, "ParentChanged", from?.ToString(),
                parentId?.ToString(), clock.GetUtcNow());
            return 0;
        }, ct);

    // ── Previews ────────────────────────────────────────────────────────────────────────────

    public async Task<LifecyclePreview> PreviewAsync(Guid communityId, string action, CancellationToken ct)
    {
        var community = await db.Communities.AsNoTracking().FirstAsync(c => c.Id == communityId, ct);
        var consequences = new Dictionary<string, object?>();
        DomainException? refusal;
        switch (action)
        {
            case "activate":
                refusal = await ActivateRefusalAsync(community, ct);
                var expiresIfSentNow = clock.GetUtcNow().AddHours(72);
                var scheduled = await db.Invitations.AsNoTracking()
                    .Where(i => i.CommunityId == communityId && i.State == InvitationState.Scheduled)
                    .OrderBy(i => i.CreatedAt)
                    .ToListAsync(ct);
                var toSend = new List<ScheduledInvitationLine>();
                var toRevoke = new List<ScheduledInvitationLine>();
                foreach (var invitation in scheduled)
                {
                    var address = await db.Properties.Where(p => p.Id == invitation.PropertyId).Select(p => p.Address).FirstOrDefaultAsync(ct);
                    if (await senderAuthority.CouldSendAsync(invitation.SenderUserId, invitation, ct))
                        toSend.Add(new(invitation.Id, invitation.Email, address, expiresIfSentNow));
                    else
                        toRevoke.Add(new(invitation.Id, invitation.Email, address, null));
                }
                consequences["scheduledInvitationsToSend"] = toSend;
                consequences["scheduledInvitationsRevoked"] = toRevoke;
                break;

            case "archive":
                refusal = await ArchiveRefusalAsync(community, ct);
                consequences["autopayCancelled"] = await db.RecurringPayments
                    .CountAsync(r => r.Status == "active" && r.Property.CommunityId == communityId, ct);
                consequences["residentsEmailed"] = await db.UserProperties
                    .Where(up => up.Property.CommunityId == communityId).Select(up => up.UserId).Distinct().CountAsync(ct);
                consequences["invitationsRevoked"] = await db.Invitations.AsNoTracking()
                    .Where(i => i.CommunityId == communityId
                                && (i.State == InvitationState.Scheduled || i.State == InvitationState.Pending))
                    .OrderBy(i => i.CreatedAt)
                    .Select(i => new RevokedInvitationLine(i.Id, i.Email, i.Kind.ToString(), i.Role.HasValue ? i.Role.Value.ToString() : null))
                    .ToListAsync(ct);
                consequences["membershipsKept"] = await EffectiveMembershipsAsync(communityId, ct);
                break;

            case "restore":
                refusal = await RestoreRefusalAsync(community, ct);
                var (to, reason) = await RestoreTargetAsync(community, ct);
                consequences["returnsTo"] = to.ToString();
                consequences["reason"] = reason;
                consequences["membershipsRegainingAccess"] = await EffectiveMembershipsAsync(communityId, ct);
                break;

            default:
                throw CompanyErrors.Validation(new CompanyErrors.FieldError("action", "action must be activate, archive or restore."));
        }

        var items = refusal?.Extensions?.TryGetValue("items", out var value) == true
            ? value as IReadOnlyList<CompanyErrors.OpenWorkItem>
            : null;
        return new LifecyclePreview(action, refusal is null,
            refusal is null ? null : new LifecycleRefusal(refusal.Code, refusal.Message, items), consequences);
    }

    // ── Rules ───────────────────────────────────────────────────────────────────────────────

    private async Task<DomainException?> ActivateRefusalAsync(Domain.Entities.Community community, CancellationToken ct)
    {
        if (community.Status == CommunityStatus.Archived)
            return CompanyErrors.CommunityArchived(community.CommunityName);
        if (community.Status == CommunityStatus.Active)
            return CompanyErrors.InvalidTransition($"{community.CommunityName} is already Active.");
        // FR-010: a pending invitation doesn't count, and neither does a manager with an end date.
        return ManagerRule.HasOpenEndedManager(await memberships.ManagerRowsAsync(community.Id, ct))
            ? null
            : CompanyErrors.NoActiveManager(community.CommunityName);
    }

    private async Task<DomainException?> ArchiveRefusalAsync(Domain.Entities.Community community, CancellationToken ct)
    {
        if (community.Status == CommunityStatus.Archived)
            return CompanyErrors.InvalidTransition($"{community.CommunityName} is already archived.");
        var work = await OpenWorkAsync(community.Id, ct);
        return work.Count == 0 ? null : CompanyErrors.OpenWork(community.CommunityName, work);
    }

    private async Task<DomainException?> RestoreRefusalAsync(Domain.Entities.Community community, CancellationToken ct)
    {
        if (community.Status != CommunityStatus.Archived)
            return CompanyErrors.InvalidTransition($"{community.CommunityName} isn't archived.");
        if (community.ParentCommunityId is { } parentId
            && await db.Communities.AsNoTracking().Where(c => c.Id == parentId && c.Status == CommunityStatus.Archived)
                .Select(c => c.CommunityName).FirstOrDefaultAsync(ct) is { } parentName)
            return CompanyErrors.ParentArchived(parentName, community.CommunityName);
        return null;
    }

    // FR-010: back to Active only if it had been live and still has an open-ended manager.
    private async Task<(CommunityStatus To, string Reason)> RestoreTargetAsync(Domain.Entities.Community community, CancellationToken ct)
    {
        if (community.ActivatedAt is null)
            return (CommunityStatus.Onboarding, "NeverActivated");
        return ManagerRule.HasOpenEndedManager(await memberships.ManagerRowsAsync(community.Id, ct))
            ? (CommunityStatus.Active, "WasActiveWithManager")
            : (CommunityStatus.Onboarding, "NoOpenEndedManager");
    }

    // FR-014: architectural applications that are Open or Decision Reached, and sub-associations
    // that aren't archived.
    private async Task<List<CompanyErrors.OpenWorkItem>> OpenWorkAsync(Guid communityId, CancellationToken ct)
    {
        var applications = await db.ArchitecturalApplications.AsNoTracking()
            .Where(a => a.CommunityId == communityId
                        && (a.Status == ArcApplicationStatus.Open || a.Status == ArcApplicationStatus.DecisionReached))
            .Select(a => new { a.ApplicationNumber, a.Status })
            .Distinct()
            .OrderBy(a => a.ApplicationNumber)
            .ToListAsync(ct);
        var subs = await db.Communities.AsNoTracking()
            .Where(c => c.ParentCommunityId == communityId && c.Status != CommunityStatus.Archived)
            .OrderBy(c => c.NormalizedName)
            .Select(c => new { c.CommunityName, c.Status })
            .ToListAsync(ct);
        return applications.Select(a => new CompanyErrors.OpenWorkItem("ArcApplication", $"ARC-{a.ApplicationNumber}",
                    a.Status == ArcApplicationStatus.Open ? "Open" : "Decision reached"))
            .Concat(subs.Select(s => new CompanyErrors.OpenWorkItem("SubAssociation", s.CommunityName, s.Status.ToString())))
            .ToList();
    }

    private Task<List<MembershipLine>> EffectiveMembershipsAsync(Guid communityId, CancellationToken ct)
    {
        var today = memberships.Today;
        return db.CommunityMemberships.AsNoTracking()
            .Where(m => m.CommunityId == communityId && m.Role != CommunityRole.Resident
                        && m.Status == MembershipStatus.Active && (m.EndDate == null || m.EndDate >= today))
            .OrderBy(m => m.Role).ThenBy(m => m.User.FirstName).ThenBy(m => m.User.LastName)
            .Select(m => new MembershipLine(m.Id, m.UserId, (m.User.FirstName + " " + m.User.LastName).Trim(),
                m.Role.ToString(), m.EndDate))
            .ToListAsync(ct);
    }

    private async Task<bool> IsDescendantAsync(Guid candidate, Guid ancestor, CancellationToken ct)
    {
        // Walk up from the candidate parent; the depth guard stops a corrupt loop.
        Guid? current = candidate;
        for (var depth = 0; current is { } id && depth < 64; depth++)
        {
            current = await db.Communities.AsNoTracking().Where(c => c.Id == id).Select(c => c.ParentCommunityId).FirstOrDefaultAsync(ct);
            if (current == ancestor)
                return true;
        }
        return false;
    }
}
