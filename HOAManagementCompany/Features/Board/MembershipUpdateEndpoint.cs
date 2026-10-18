using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using FastEndpoints;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Board.Settings;
using HOAManagementCompany.Features.Company;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Board;

// PATCH /api/v1/communities/{communityId}/memberships/{membershipId} — end, downgrade or change
// the end date of a membership (025 US5; 030 FR-019, FR-020, contracts/community-settings.md).
// The open-ended-manager rule decides LAST_MANAGER under the community lock, so concurrent ends
// can't both win (replacing 025's racy count). While the community is archived only a Company
// Administrator's end is allowed, and the rule is skipped (FR-012). Ending or downgrading a
// manager revokes the invitations they can no longer send. Takes effect on the next request.
public class MembershipUpdateEndpoint(
    ApplicationDbContext db,
    ICommunityScopeResolver scope,
    MembershipService memberships)
    : Endpoint<MembershipUpdateRequest, MembershipDto>
{
    public override void Configure()
    {
        Patch("/communities/{communityId}/memberships/{membershipId}");
        // 030 FR-041a: per-user write limit, which 025 lacked here.
        Description(x => x.WithName("UpdateMembership").WithTags("Board").RequireRateLimiting("board-writes"));
    }

    public override async Task HandleAsync(MembershipUpdateRequest req, CancellationToken ct)
    {
        BoardHttp.NoStore(HttpContext);
        // Refuse before revealing whether the membership exists.
        if (await BoardHttp.SendDecisionAsync(HttpContext,
                await scope.DecideAsync(User, req.CommunityId, CommunityCapability.ManageMemberships, ScopeOperation.Read, ct), ct))
            return;

        var changes = (req.End == true ? 1 : 0) + (req.DowngradeTo is null ? 0 : 1)
                      + (req.EndDate.ValueKind == JsonValueKind.Undefined ? 0 : 1);
        if (changes != 1)
            throw CompanyErrors.Validation(new CompanyErrors.FieldError("end",
                "Send one change at a time: end, downgradeTo or endDate."));
        var endDate = ParseEndDate(req.EndDate);
        var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value;

        var dto = await CommunityLocks.RunAsync(db, req.CommunityId, async () =>
        {
            var row = await db.CommunityMemberships
                          .FirstOrDefaultAsync(x => x.Id == req.MembershipId && x.CommunityId == req.CommunityId, ct)
                      ?? throw new DomainException("NOT_FOUND", "Membership not found in this community.", StatusCodes.Status404NotFound);
            if (row.Role == CommunityRole.Resident)
                throw CompanyErrors.Validation(new CompanyErrors.FieldError("role", "Resident memberships can't be changed here."));

            // The rule first: the loser of two concurrent ends has lost their own role by now, and
            // must hear LAST_MANAGER rather than FORBIDDEN (US2-S14). Archived communities skip it.
            if (!await db.Communities.AnyAsync(c => c.Id == req.CommunityId && c.Status == CommunityStatus.Archived, ct))
                await memberships.EnsureManagerRuleAsync(row,
                    req.End == true ? "end" : req.DowngradeTo is not null ? "downgrade" : "endDate", endDate, ct);

            var capability = row.Role == CommunityRole.CommunityManager
                ? CommunityCapability.ManageCommunityManagers
                : CommunityCapability.ManageMemberships;
            var decision = await scope.DecideAsync(User, req.CommunityId, capability, ScopeOperation.Write, ct);
            var archived = false;
            if (decision == ScopeDecision.Archived && req.End == true
                && await scope.DecideAsync(User, req.CommunityId, CommunityCapability.ManageCommunityLifecycle,
                    ScopeOperation.Write, ct) == ScopeDecision.Allowed)
                (decision, archived) = (ScopeDecision.Allowed, true);
            if (BoardHttp.RefusalFor(decision) is { } refusal)
                throw refusal;

            if (req.End == true)
                await memberships.EndAsync(row, archived, actorId, "end", null, ct);
            else if (req.DowngradeTo is { } downgradeTo)
            {
                if (row.Role != CommunityRole.CommunityManager || downgradeTo != CommunityRole.BoardMember)
                    throw CompanyErrors.Validation(new CompanyErrors.FieldError("downgradeTo",
                        "Only a Community Manager can be downgraded, and only to Board Member."));
                row = await memberships.DowngradeAsync(row, actorId, ct);
            }
            else
                await memberships.SetEndDateAsync(row, endDate, actorId, ct);

            var (name, _) = await memberships.NamesAsync(row.UserId, req.CommunityId, ct);
            return memberships.ToDto(row, name);
        }, ct);
        await SendOkAsync(dto, ct);
    }

    // { "endDate": "2026-12-31" } sets or moves it; { "endDate": null } clears it.
    private static DateOnly? ParseEndDate(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null => null,
        JsonValueKind.String when DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date) => date,
        _ => throw CompanyErrors.Validation(new CompanyErrors.FieldError("endDate", "Enter a real date as YYYY-MM-DD.")),
    };
}
