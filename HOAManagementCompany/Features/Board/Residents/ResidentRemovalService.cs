using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Residents;

/// <summary>What removing one resident does to the home's autopay: the lines the remove dialog shows.</summary>
public sealed record RemovalPreview(string Autopay, string? AutopayReason, int OtherResidentsEmailed);

public sealed record RemovalResult(bool Removed, bool AutopayCancelled);

/// <summary>
/// Removing a resident from a home (030 FR-024c, R9). The link goes; the home's autopay is
/// cancelled when this resident enrolled it or no enroller is recorded; the removed resident is
/// emailed, and so is each other resident when autopay ended. The owner of record, the balance and
/// the user's other homes and memberships are untouched. Runs inside the caller's community lock.
/// </summary>
public sealed class ResidentRemovalService(
    ApplicationDbContext db,
    PaymentService payments,
    CompanyEmailRenderer emails,
    TimeProvider clock,
    ILogger<ResidentRemovalService> logger)
{
    public const string WillCancel = "WillCancel";
    public const string Unaffected = "Unaffected";
    public const string NoAutopay = "None";

    public async Task<RemovalPreview> PreviewAsync(Guid propertyId, string userId, CancellationToken ct)
    {
        await RequireLinkAsync(propertyId, userId, ct);
        var (autopay, reason) = await ClassifyAsync(propertyId, userId, ct);
        var others = autopay == WillCancel ? (await OtherResidentsAsync(propertyId, userId, ct)).Count : 0;
        return new RemovalPreview(autopay, reason, others);
    }

    public async Task<RemovalResult> RemoveAsync(Guid communityId, Guid propertyId, string userId, string actorId,
        CancellationToken ct)
    {
        var link = await RequireLinkAsync(propertyId, userId, ct);
        var now = clock.GetUtcNow();
        var (autopay, _) = await ClassifyAsync(propertyId, userId, ct);

        db.UserProperties.Remove(link);
        var cancelled = autopay == WillCancel
            ? await payments.CancelRecurringForAsync(propertyId, RecurringCancellationReasons.ResidentRemoved, ct)
            : null;

        var home = await db.Properties.AsNoTracking().Where(p => p.Id == propertyId)
            .Select(p => new { p.Address, p.Community.CommunityName }).FirstAsync(ct);
        var removed = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Email).FirstAsync(ct);
        if (!string.IsNullOrEmpty(removed))
            db.OutboxMessages.Add(emails.ResidentRemoved(userId, removed, propertyId, home.Address, home.CommunityName,
                cancelled is not null, now));
        if (cancelled is not null)
            foreach (var other in await OtherResidentsAsync(propertyId, userId, ct))
                db.OutboxMessages.Add(emails.AutopayEnded(other.UserId, other.Email, cancelled.Id, home.Address, now));

        await db.SaveChangesAsync(ct);
        CompanyLog.ResidentLinkChange(logger, actorId, communityId, propertyId, userId, "Removed", cause: null, now);
        if (cancelled is not null)
            CompanyLog.AutopayCancelled(logger, actorId, propertyId, cancelled.Id, RecurringCancellationReasons.ResidentRemoved,
                "ResidentRemoved", now);
        return new RemovalResult(true, cancelled is not null);
    }

    private async Task<UserProperty> RequireLinkAsync(Guid propertyId, string userId, CancellationToken ct) =>
        await db.UserProperties.FirstOrDefaultAsync(up => up.PropertyId == propertyId && up.UserId == userId, ct)
        ?? throw new Auth.DomainException("NOT_FOUND", "That person isn't a resident of this home.", StatusCodes.Status404NotFound);

    private async Task<(string Autopay, string? Reason)> ClassifyAsync(Guid propertyId, string userId, CancellationToken ct)
    {
        var schedule = await db.RecurringPayments.AsNoTracking()
            .Where(r => r.PropertyId == propertyId && r.Status == "active")
            .Select(r => new { r.EnrolledByUserId })
            .FirstOrDefaultAsync(ct);
        return schedule switch
        {
            null => (NoAutopay, null),
            { EnrolledByUserId: null } => (WillCancel, "NoEnrollerRecorded"),
            { EnrolledByUserId: var enroller } when enroller == userId => (WillCancel, "EnrolledByThisResident"),
            _ => (Unaffected, "EnrolledByAnotherResident"),
        };
    }

    private async Task<List<(string UserId, string Email)>> OtherResidentsAsync(Guid propertyId, string userId, CancellationToken ct) =>
        (await db.UserProperties.AsNoTracking()
            .Where(up => up.PropertyId == propertyId && up.UserId != userId && up.User.Email != null && up.User.Email != "")
            .Select(up => new { up.UserId, up.User.Email })
            .ToListAsync(ct))
        .Select(x => (x.UserId, x.Email!))
        .ToList();
}
