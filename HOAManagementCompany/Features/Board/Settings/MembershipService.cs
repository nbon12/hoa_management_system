using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board.Settings;

// <!-- REPOWISE:START domain=board -->
// Membership changes shared by the membership endpoints, the one-action replace and the
// membership invitation offer (030 FR-018–FR-021; research R7). Every method runs inside the
// caller's community lock. The open-ended-manager rule (ManagerRule, FR-020) is decided on the
// community's manager rows read inside that lock, so two concurrent ends can't both win. Appointing
// someone who held the role before reactivates their row; a downgrade ends the manager row and
// reactivates or creates the Board Member row. Ending or downgrading a manager revokes the
// invitations they can no longer send.
// <!-- REPOWISE:END -->
public sealed class MembershipService(
    ApplicationDbContext db,
    SenderAuthority senderAuthority,
    TimeProvider clock,
    ILogger<MembershipService> logger)
{
    public const string NotInPeopleList = "Choose someone from this community, or invite them by email.";

    public DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    /// <summary>True when the membership confers its role today (025: Active and not past its end date).</summary>
    public static bool IsEffective(CommunityMembership m, DateOnly today) =>
        m.Status == MembershipStatus.Active && (m.EndDate is null || m.EndDate >= today);

    public Task<List<ManagerRule.Row>> ManagerRowsAsync(Guid communityId, CancellationToken ct) =>
        db.CommunityMemberships
            .Where(m => m.CommunityId == communityId && m.Role == CommunityRole.CommunityManager)
            .Select(m => new ManagerRule.Row(m.Id, m.Status, m.EndDate))
            .ToListAsync(ct);

    /// <summary>
    /// FR-018: the people a membership can be granted to directly — users with an effective
    /// membership or a resident link in this community.
    /// </summary>
    public async Task<bool> InPeopleListAsync(Guid communityId, string userId, CancellationToken ct)
    {
        var today = Today;
        return await db.CommunityMemberships.AnyAsync(m => m.CommunityId == communityId && m.UserId == userId
                                                           && m.Status == MembershipStatus.Active
                                                           && (m.EndDate == null || m.EndDate >= today), ct)
               || await db.UserProperties.AnyAsync(up => up.UserId == userId && up.Property.CommunityId == communityId, ct);
    }

    /// <summary>
    /// Grants <paramref name="role"/> to <paramref name="userId"/>, reactivating their previous row
    /// for the role when there is one (R7). Refuses <c>ALREADY_MEMBER</c> when they hold it today,
    /// unless <paramref name="tolerateHeld"/> (invitation acceptance), which then changes nothing.
    /// A Community Manager grant goes through the open-ended-manager rule. The caller saves.
    /// </summary>
    public async Task<(CommunityMembership Row, bool AlreadyHeld)> GrantAsync(Guid communityId, string userId,
        CommunityRole role, DateOnly startDate, DateOnly? endDate, string actorId, string kind, string? cause,
        bool tolerateHeld, CancellationToken ct)
    {
        var today = Today;
        var existing = await db.CommunityMemberships
            .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.UserId == userId && m.Role == role, ct);
        if (existing is not null && IsEffective(existing, today))
        {
            if (tolerateHeld)
                return (existing, true);
            var (name, community) = await NamesAsync(userId, communityId, ct);
            throw CompanyErrors.AlreadyMember($"{name} is already {WithArticle(role)} of {community}.");
        }

        if (role == CommunityRole.CommunityManager
            && !ManagerRule.Evaluate(await ManagerRowsAsync(communityId, ct), ManagerRule.Change.Appoint(endDate), today,
                archived: false))
        {
            var (_, community) = await NamesAsync(userId, communityId, ct);
            throw CompanyErrors.LastManager(
                $"{community} has no Community Manager with no end date yet, so its first manager can't have an end date. Appoint them with no end date.");
        }

        var now = clock.GetUtcNow();
        if (existing is null)
        {
            existing = new CommunityMembership
            {
                Id = Guid.NewGuid(), UserId = userId, CommunityId = communityId, Role = role,
                Status = MembershipStatus.Active, StartDate = startDate, EndDate = endDate,
            };
            db.CommunityMemberships.Add(existing);
        }
        else
        {
            existing.Status = MembershipStatus.Active;
            existing.StartDate = startDate;
            existing.EndDate = endDate;
            kind = kind == "grant" ? "reactivate" : kind;
        }

        CompanyLog.MembershipChange(logger, actorId, communityId, existing.Id, userId, role.ToString(), kind, cause, now);
        return (existing, false);
    }

    /// <summary>
    /// Ends a membership today (Status Inactive). A manager end goes through the open-ended rule,
    /// except while the community is archived (FR-012, FR-020). Saves, then revokes the invitations
    /// an ended manager can no longer send, in the same transaction.
    /// </summary>
    public async Task EndAsync(CommunityMembership row, bool archived, string actorId, string kind, string? cause,
        CancellationToken ct)
    {
        if (row.Role == CommunityRole.CommunityManager)
            await RequireRuleAsync(row, ManagerRule.Change.End(row.Id), archived, EndMessage, ct);

        row.Status = MembershipStatus.Inactive;
        row.EndDate = Today;
        await db.SaveChangesAsync(ct);
        CompanyLog.MembershipChange(logger, actorId, row.CommunityId, row.Id, row.UserId, row.Role.ToString(), kind, cause,
            clock.GetUtcNow());
        if (row.Role == CommunityRole.CommunityManager)
            await RevokeOrphanedAsync(row, actorId, cause ?? kind, ct);
    }

    /// <summary>
    /// FR-019 downgrade: ends the manager row and reactivates or creates the Board Member row,
    /// open-ended. Returns the Board Member row.
    /// </summary>
    public async Task<CommunityMembership> DowngradeAsync(CommunityMembership managerRow, string actorId, CancellationToken ct)
    {
        await RequireRuleAsync(managerRow, ManagerRule.Change.Downgrade(managerRow.Id), archived: false, DowngradeMessage, ct);

        var cause = $"downgrade:{managerRow.Id}";
        var today = Today;
        managerRow.Status = MembershipStatus.Inactive;
        managerRow.EndDate = today;
        CompanyLog.MembershipChange(logger, actorId, managerRow.CommunityId, managerRow.Id, managerRow.UserId,
            managerRow.Role.ToString(), "downgrade", cause, clock.GetUtcNow());
        var (board, _) = await GrantAsync(managerRow.CommunityId, managerRow.UserId, CommunityRole.BoardMember, today, null,
            actorId, "grant", cause, tolerateHeld: true, ct);
        await db.SaveChangesAsync(ct);
        await RevokeOrphanedAsync(managerRow, actorId, cause, ct);
        return board;
    }

    /// <summary>Sets, moves or clears an end date. A manager's goes through the open-ended rule.</summary>
    public async Task SetEndDateAsync(CommunityMembership row, DateOnly? endDate, string actorId, CancellationToken ct)
    {
        if (endDate is { } end && end < row.StartDate)
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("endDate", "The end date can't be before the start date."));
        if (row.Role == CommunityRole.CommunityManager)
            await RequireRuleAsync(row, ManagerRule.Change.SetEndDate(row.Id, endDate), archived: false, EndDateMessage, ct);

        row.EndDate = endDate;
        await db.SaveChangesAsync(ct);
        CompanyLog.MembershipChange(logger, actorId, row.CommunityId, row.Id, row.UserId, row.Role.ToString(), "edit", null,
            clock.GetUtcNow());
        // A manager whose term now ends in the past can no longer send.
        if (row.Role == CommunityRole.CommunityManager && !IsEffective(row, Today))
            await RevokeOrphanedAsync(row, actorId, "edit", ct);
    }

    /// <summary>
    /// The open-ended-manager rule for a change to a manager row, with the message for that change
    /// ("end", "downgrade" or "endDate"). The membership endpoint runs it inside the lock before
    /// re-checking the caller, so the loser of two concurrent ends hears LAST_MANAGER (US2-S14).
    /// </summary>
    public Task EnsureManagerRuleAsync(CommunityMembership row, string change, DateOnly? endDate, CancellationToken ct) =>
        row.Role != CommunityRole.CommunityManager
            ? Task.CompletedTask
            : change switch
            {
                "end" => RequireRuleAsync(row, ManagerRule.Change.End(row.Id), false, EndMessage, ct),
                "downgrade" => RequireRuleAsync(row, ManagerRule.Change.Downgrade(row.Id), false, DowngradeMessage, ct),
                _ => RequireRuleAsync(row, ManagerRule.Change.SetEndDate(row.Id, endDate), false, EndDateMessage, ct),
            };

    private static string EndMessage(string name, string community) =>
        $"{name} is the only Community Manager of {community} with no end date. A community must always have one. Appoint another manager first.";

    private static string DowngradeMessage(string name, string community) =>
        $"{name} can't be downgraded. They're the only Community Manager of {community} with no end date. Appoint another manager first.";

    private static string EndDateMessage(string name, string community) =>
        $"{name} can't be given an end date while they're the only Community Manager of {community} with no end date.";

    public async Task<(string Name, string Community)> NamesAsync(string userId, Guid communityId, CancellationToken ct)
    {
        var name = await db.Users.Where(u => u.Id == userId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync(ct);
        var community = await db.Communities.Where(c => c.Id == communityId).Select(c => c.CommunityName).FirstAsync(ct);
        return ((name ?? "This person").Trim(), community);
    }

    public static string RoleLabel(CommunityRole role) => Invitations.InvitationEmailRenderer.RoleLabel(role);

    public static string WithArticle(CommunityRole role) =>
        role == CommunityRole.Accountant ? $"an {RoleLabel(role)}" : $"a {RoleLabel(role)}";

    public MembershipDto ToDto(CommunityMembership m, string name) =>
        new(m.Id, m.UserId, name, m.Role.ToString(), m.Status.ToString(), m.StartDate, m.EndDate);

    private async Task RequireRuleAsync(CommunityMembership row, ManagerRule.Change change, bool archived,
        Func<string, string, string> message, CancellationToken ct)
    {
        if (ManagerRule.Evaluate(await ManagerRowsAsync(row.CommunityId, ct), change, Today, archived))
            return;
        var (name, community) = await NamesAsync(row.UserId, row.CommunityId, ct);
        throw CompanyErrors.LastManager(message(name, community));
    }

    private async Task RevokeOrphanedAsync(CommunityMembership managerRow, string actorId, string cause, CancellationToken ct)
    {
        if (await senderAuthority.RevokeOrphanedAsync(managerRow.UserId, managerRow.CommunityId, actorId, cause, ct) > 0)
            await db.SaveChangesAsync(ct);
    }
}
