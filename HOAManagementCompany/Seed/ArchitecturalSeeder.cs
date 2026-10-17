using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Seed;

/// <summary>
/// Demo data for board architectural review (027 research R11). Idempotent and run on every
/// Dev/Development startup after <see cref="AuthSeeder.EnsureBoardUserAsync"/>: ensures a five-member
/// board (board@ plus board2–5@nekohoa.dev), a community manager (manager@nekohoa.dev), ARC settings,
/// and applications mirroring the wireframe — including one denied v1 with an open v2 and ARC-1042,
/// which board@nekohoa.dev has not voted on.
/// </summary>
public class ArchitecturalSeeder(ApplicationDbContext db, IServiceProvider services, ILogger logger)
{
    private const string CommunityName = "Sakura Heights HOA";
    private const string Password = "Password1!";
    private const string BoardEmail = "board@nekohoa.dev";
    private const string ManagerEmail = "manager@nekohoa.dev";
    private const string RegistrationAccount = "SAKURA-003";
    private const string ResidentEmail = "resident@nekohoa.dev";

    private static readonly (string Email, string First, string Last)[] ExtraBoard =
    [
        ("board2@nekohoa.dev", "Aaliyah", "Brooks"),
        ("board3@nekohoa.dev", "Marcus", "Chen"),
        ("board4@nekohoa.dev", "Priya", "Raman"),
        ("board5@nekohoa.dev", "Tomás", "Alvarez"),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var community = await db.Communities.FirstOrDefaultAsync(c => c.CommunityName == CommunityName, ct);
        var boardUser = await db.Users.FirstOrDefaultAsync(u => u.Email == BoardEmail, ct);
        if (community is null || boardUser is null)
        {
            logger.LogWarning("ArchitecturalSeeder skipped — seeded community or board user missing.");
            return;
        }

        // Login needs a linked property; co-home the extra demo users on board@'s property, which the
        // seeded applications never use, so nobody is recused.
        var homeProperty = await db.UserProperties.Where(up => up.UserId == boardUser.Id)
            .Select(up => (Guid?)up.PropertyId).FirstOrDefaultAsync(ct);
        if (homeProperty is null)
        {
            logger.LogWarning("ArchitecturalSeeder skipped — board user has no linked property.");
            return;
        }

        var boardIds = new List<string> { boardUser.Id };
        foreach (var (email, first, last) in ExtraBoard)
            boardIds.Add(await EnsureUserWithRoleAsync(community.Id, homeProperty.Value, email, first, last, CommunityRole.BoardMember, ct));
        await EnsureUserWithRoleAsync(community.Id, homeProperty.Value, ManagerEmail, "Morgan", "Manager", CommunityRole.CommunityManager, ct);

        // Independent of the board demo below, so it also lands on dev databases seeded before 029.
        await EnsureResidentDemoAsync(boardUser.Id, ct);

        if (await db.ArchitecturalApplications.AnyAsync(a => a.CommunityId == community.Id
                                                             && a.SubmittedByUserId == null, ct))
            return;

        // Properties the board user isn't linked to (so they aren't recused) and that aren't the
        // registration e2e property.
        var boardLinked = await db.UserProperties.Where(up => up.UserId == boardUser.Id)
            .Select(up => up.PropertyId).ToListAsync(ct);
        var properties = await db.Properties
            .Where(p => p.CommunityId == community.Id && p.AccountNumber != RegistrationAccount
                        && !boardLinked.Contains(p.Id))
            .OrderBy(p => p.AccountNumber)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (properties.Count == 0)
        {
            logger.LogWarning("ArchitecturalSeeder skipped applications — no eligible property.");
            return;
        }
        Guid Property(int i) => properties[i % properties.Count];

        var factory = services.GetRequiredService<ArcApplicationFactory>();
        await factory.GetOrCreateSettingsAsync(community.Id, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var manager = await db.Users.FirstAsync(u => u.Email == ManagerEmail, ct);

        // ARC-1036: decided and closed (approved).
        var shed = await CreateAsync(factory, 1036, Property(0), "Daniel Koontz", ArcProjectType.Outbuilding,
            "Detached shed, 10x12", "Prefabricated 10x12 shed in the rear yard, painted to match the house.",
            today.AddDays(-36), [], ct);
        Votes(shed, boardIds, ArcVoteChoice.Approve, ArcVoteChoice.Approve, ArcVoteChoice.Approve, ArcVoteChoice.Approve);
        Close(shed, ArcOutcome.Approved, null, null, manager.Id, today.AddDays(-30));

        // ARC-1039 v1: denied with "revisions requested", then resubmitted as an open v2.
        var paintV1 = await CreateAsync(factory, 1039, Property(1), "Hasan Mehdi", ArcProjectType.ExteriorPaint,
            "Exterior repaint — Sage 4021", "Repaint siding and trim. Color chip attached.",
            today.AddDays(-50), [("color-chip.png", "image/png")], ct);
        Votes(paintV1, boardIds, ArcVoteChoice.RevisionsNeeded, ArcVoteChoice.RevisionsNeeded, ArcVoteChoice.Approve, ArcVoteChoice.Deny);
        Close(paintV1, ArcOutcome.Denied, ArcDenialWording.RevisionsRequested,
            "Choose a trim color from the approved palette (Guideline 6.1).", manager.Id, today.AddDays(-40));
        await db.SaveChangesAsync(ct);

        var paintV2 = await factory.CreateRevisionAsync(paintV1.Id, new ArcNewApplication(
            paintV1.PropertyId, paintV1.OwnerName, ArcProjectType.ExteriorPaint, "Exterior repaint — Sage 4021",
            "Repaint siding Sage 4021; trim now Swiss Coffee from the approved palette.",
            today.AddDays(-14), null, []), [], ct);
        Votes(paintV2, boardIds, ArcVoteChoice.Approve, ArcVoteChoice.Approve);

        // ARC-1041: open; board@ already voted.
        var solar = await CreateAsync(factory, 1041, Property(2), "Stephanie H Ross", ArcProjectType.Solar,
            "Solar panel array, rear roof", "14-panel array on the rear roof plane, not visible from the street.",
            today.AddDays(-16), [("solar-layout.pdf", "application/pdf"), ("spec-sheet.pdf", "application/pdf")], ct);
        Votes(solar, boardIds, ArcVoteChoice.Approve, ArcVoteChoice.Deny);

        // ARC-1042: open; board@ has NOT voted ("Needs your vote").
        var fence = await CreateAsync(factory, 1042, Property(3), "Praneeth Pattyam", ArcProjectType.Fence,
            "Fence replacement — 6ft cedar", "Replace the existing wood fence with 6ft cedar along the rear lot line.",
            today.AddDays(-9),
            [("fence-plan.pdf", "application/pdf"), ("elevation.jpg", "image/jpeg"), ("plat-survey.pdf", "application/pdf")], ct);
        Votes(fence, boardIds.Skip(1).ToList(), ArcVoteChoice.Approve, ArcVoteChoice.Approve);

        var settings = await db.CommunityArcSettings.FirstAsync(s => s.CommunityId == community.Id, ct);
        settings.NextApplicationNumber = Math.Max(settings.NextApplicationNumber, 1043);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded architectural applications for {Community}.", CommunityName);
    }

    /// <summary>
    /// 029 quickstart demo for resident@nekohoa.dev's own property: one open request with an unanswered
    /// board question (dashboard alert + reply flow) and one denied request (revise and resubmit).
    /// Idempotent: skipped once the resident has any submitted request.
    /// </summary>
    private async Task EnsureResidentDemoAsync(string boardUserId, CancellationToken ct)
    {
        var resident = await db.Users.FirstOrDefaultAsync(u => u.Email == ResidentEmail, ct);
        if (resident is null)
            return;
        var propertyId = await db.UserProperties.Where(up => up.UserId == resident.Id)
            .Select(up => (Guid?)up.PropertyId).FirstOrDefaultAsync(ct);
        if (propertyId is null
            || await db.ArchitecturalApplications.AnyAsync(a => a.SubmittedByUserId == resident.Id, ct))
            return;

        var factory = services.GetRequiredService<ArcApplicationFactory>();
        var managerId = await db.Users.Where(u => u.Email == ManagerEmail).Select(u => u.Id).FirstAsync(ct);
        var ownerName = await db.Owners.Where(o => o.PropertyId == propertyId)
            .Select(o => o.FirstName + " " + o.LastName).FirstOrDefaultAsync(ct)
            ?? $"{resident.FirstName} {resident.LastName}";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;

        var open = await factory.CreateFromSettingsAsync(new ArcNewApplication(
            propertyId.Value, ownerName, ArcProjectType.Landscaping, "Front-yard xeriscape",
            "Replace the front lawn with drought-tolerant beds and a gravel path.", today.AddDays(-6), resident.Id, [],
            PlannedStartDate: today.AddDays(20), PlannedCompletionDate: today.AddDays(35), AcknowledgedAt: now.AddDays(-6)), ct);
        db.ArchitecturalInfoRequests.Add(new ArchitecturalInfoRequest
        {
            ApplicationId = open.Id, RequestedByUserId = boardUserId, RequestedAt = now.AddDays(-2),
            Message = "Please attach a planting plan showing plant species and spacing."
        });

        var denied = await factory.CreateFromSettingsAsync(new ArcNewApplication(
            propertyId.Value, ownerName, ArcProjectType.WindowsDoors, "Front door replacement — red fiberglass",
            "Replace the front door with a red fiberglass door.", today.AddDays(-40), resident.Id, [],
            PlannedStartDate: today.AddDays(-10), PlannedCompletionDate: today.AddDays(-5), AcknowledgedAt: now.AddDays(-40)), ct);
        Close(denied, ArcOutcome.Denied, ArcDenialWording.RevisionsRequested,
            "Choose a door color from the approved palette (Guideline 7.2).", managerId, today.AddDays(-25));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded resident architectural demo requests.");
    }

    private async Task<ArchitecturalApplication> CreateAsync(
        ArcApplicationFactory factory, int number, Guid propertyId, string owner, ArcProjectType type,
        string title, string description, DateOnly received, (string Name, string ContentType)[] files,
        CancellationToken ct)
    {
        var communityId = await db.Properties.Where(p => p.Id == propertyId).Select(p => p.CommunityId).FirstAsync(ct);
        var attachments = new List<ArcNewAttachment>();
        foreach (var (name, contentType) in files)
        {
            var key = $"arc/{communityId}/{number}/{Guid.NewGuid()}";
            var bytes = await UploadAsync(key, contentType, ct);
            attachments.Add(new ArcNewAttachment(name, bytes, contentType, key));
        }

        // Snapshot the property's real owner when there is one, so emails and the board view agree.
        var realOwner = await db.Owners.Where(o => o.PropertyId == propertyId)
            .Select(o => o.FirstName + " " + o.LastName).FirstOrDefaultAsync(ct);
        var app = await factory.CreateFromSettingsAsync(new ArcNewApplication(
            propertyId, string.IsNullOrWhiteSpace(realOwner) ? owner : realOwner, type, title, description, received, null, attachments), ct);
        app.ApplicationNumber = number;
        await db.SaveChangesAsync(ct);
        return app;
    }

    private async Task<long> UploadAsync(string key, string contentType, CancellationToken ct)
    {
        var content = await TestDataFiles.ReadSamplePdfAsync(ct);
        try
        {
            await services.GetRequiredService<IDocumentStorage>().UploadAsync(key, content, contentType, ct);
        }
        catch (Exception ex)
        {
            // Non-fatal: the board UI shows "attachment unavailable" for a missing object.
            logger.LogWarning(ex, "ArchitecturalSeeder could not upload a demo attachment.");
        }
        return content.LongLength;
    }

    private void Votes(ArchitecturalApplication app, IReadOnlyList<string> voters, params ArcVoteChoice[] choices)
    {
        for (var i = 0; i < choices.Length && i < voters.Count; i++)
            db.ArchitecturalVotes.Add(new ArchitecturalVote
            {
                ApplicationId = app.Id,
                VoterUserId = voters[i],
                Choice = choices[i],
                CastAt = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(i)
            });
    }

    private static void Close(ArchitecturalApplication app, ArcOutcome outcome, ArcDenialWording? wording,
        string? reason, string managerId, DateOnly closedOn)
    {
        var at = new DateTimeOffset(closedOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        app.Status = ArcApplicationStatus.Closed;
        app.DecisionOutcome = outcome;
        app.DecisionWording = wording;
        app.DecisionSource = ArcDecisionSource.Votes;
        app.DecisionReachedAt = at;
        app.OwnerReason = reason;
        app.ClosedAt = at;
        app.ClosedByUserId = managerId;
        app.LapseProcessedAt = at;
        app.ReminderSentAt = at;
    }

    private async Task<string> EnsureUserWithRoleAsync(
        Guid communityId, Guid homePropertyId, string email, string first, string last, CommunityRole role, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Email = email, UserName = email, FirstName = first, LastName = last,
                EmailConfirmed = true, LockoutEnabled = true
            };
            var result = await services.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, Password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Could not create seed user {email}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        if (!await db.UserProperties.AnyAsync(up => up.UserId == user.Id, ct))
        {
            db.UserProperties.Add(new UserProperty { UserId = user.Id, PropertyId = homePropertyId });
            await db.SaveChangesAsync(ct);
        }

        if (!await db.CommunityMemberships.AnyAsync(
                m => m.UserId == user.Id && m.CommunityId == communityId && m.Role == role, ct))
        {
            db.CommunityMemberships.Add(new CommunityMembership
            {
                UserId = user.Id,
                CommunityId = communityId,
                Role = role,
                Status = MembershipStatus.Active,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1))
            });
            await db.SaveChangesAsync(ct);
        }
        return user.Id;
    }
}
