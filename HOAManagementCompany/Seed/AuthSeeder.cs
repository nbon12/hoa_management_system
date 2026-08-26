using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HOAManagementCompany.Seed;

public class AuthSeeder(ApplicationDbContext db, IServiceProvider services, ILogger logger)
{
    private const string PrimaryEmail = "resident@nekohoa.dev";
    private const string SecondaryEmail = "resident2@nekohoa.dev";
    private const string BoardEmail = "board@nekohoa.dev";
    private const string Password = "Password1!";
    private const string CommunityName = "Sakura Heights HOA";

    public async Task<bool> ShouldSeedAsync(CancellationToken ct = default)
        => !await db.Users.AnyAsync(u => u.Email == PrimaryEmail, ct);

    // 025: idempotently ensure a board-eligible user (board@nekohoa.dev) exists so the
    // board-mode journey can be exercised in dev/E2E — even on an already-seeded database
    // (e.g. the pr-env fork, where the full seed no-ops via ShouldSeedAsync). Attaches the
    // user to an existing property in the seeded community and grants an active BoardMember
    // membership. Safe to run every startup.
    // The e2e registration flow (E2EClaimCodeEndpoint) claims this seed property, so it MUST
    // stay unclaimed — the board user must never hold a link to it.
    private const string E2eRegistrationAccount = "SAKURA-003";

    public async Task EnsureBoardUserAsync(CancellationToken ct = default)
    {
        var community = await db.Communities.FirstOrDefaultAsync(c => c.CommunityName == CommunityName, ct);
        if (community is null)
        {
            logger.LogWarning("EnsureBoardUserAsync skipped — no seeded community found.");
            return;
        }

        var regProperty = await db.Properties
            .FirstOrDefaultAsync(p => p.AccountNumber == E2eRegistrationAccount, ct);

        // A safe co-residence property: already-claimed, in the community, and NOT the
        // registration seed property.
        var safeProperty = await db.Properties.FirstOrDefaultAsync(
            p => p.CommunityId == community.Id
                 && p.AccountNumber != E2eRegistrationAccount
                 && db.UserProperties.Any(up => up.PropertyId == p.Id), ct);
        if (safeProperty is null)
        {
            logger.LogWarning("EnsureBoardUserAsync skipped — no safe claimed property to attach the board user to.");
            return;
        }

        var boardUser = await db.Users.FirstOrDefaultAsync(u => u.Email == BoardEmail, ct);
        if (boardUser is not null)
        {
            // Self-heal: an earlier build may have linked the board user to the registration
            // property (SAKURA-003), which blocks the registration e2e. Free it and re-home
            // the board user on the safe property.
            if (regProperty is not null)
            {
                var stale = await db.UserProperties
                    .Where(up => up.UserId == boardUser.Id && up.PropertyId == regProperty.Id)
                    .ToListAsync(ct);
                if (stale.Count > 0)
                {
                    db.UserProperties.RemoveRange(stale);
                    if (!await db.UserProperties.AnyAsync(up => up.UserId == boardUser.Id && up.PropertyId == safeProperty.Id, ct))
                        db.UserProperties.Add(new UserProperty { UserId = boardUser.Id, PropertyId = safeProperty.Id });
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation("Repaired board user property link — freed {Account}.", E2eRegistrationAccount);
                }
            }
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var newUser = new ApplicationUser
        {
            Email = BoardEmail,
            UserName = BoardEmail,
            FirstName = "Bianca",
            LastName = "Board",
            EmailConfirmed = true,
            LockoutEnabled = true
        };
        var result = await userManager.CreateAsync(newUser, Password);
        if (!result.Succeeded)
        {
            logger.LogWarning("EnsureBoardUserAsync could not create the board user: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        db.UserProperties.Add(new UserProperty { UserId = newUser.Id, PropertyId = safeProperty.Id });
        db.CommunityMemberships.Add(new CommunityMembership
        {
            UserId = newUser.Id,
            CommunityId = community.Id,
            Role = CommunityRole.BoardMember,
            Status = MembershipStatus.Active,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1))
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Ensured board-eligible seed user {Email}.", BoardEmail);
    }

    public async Task<SeedResult> SeedAsync(CancellationToken ct = default)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var primaryUser = new ApplicationUser
        {
            Email = PrimaryEmail,
            UserName = PrimaryEmail,
            FirstName = "Jane",
            LastName = "Resident",
            EmailConfirmed = true,
            LockoutEnabled = true
        };
        await userManager.CreateAsync(primaryUser, Password);

        var secondaryUser = new ApplicationUser
        {
            Email = SecondaryEmail,
            UserName = SecondaryEmail,
            FirstName = "John",
            LastName = "Resident",
            EmailConfirmed = true,
            LockoutEnabled = true
        };
        await userManager.CreateAsync(secondaryUser, Password);

        // Ensure exactly one Community row (the tenant boundary) exists for the seed data.
        var community = await db.Communities.FirstOrDefaultAsync(c => c.CommunityName == CommunityName, ct);
        if (community is null)
        {
            community = new Community
            {
                CommunityName = CommunityName,
                LegalName = CommunityName,
                Status = CommunityStatus.Active
            };
            db.Communities.Add(community);
            await db.SaveChangesAsync(ct);
        }

        var primaryProperty = new Property
        {
            Id = Guid.NewGuid(),
            AccountNumber = "SAKURA-001",
            CommunityId = community.Id,
            Address = "1 Sakura Drive",
            City = "San Jose",
            State = "CA",
            Zip = "95101",
            Lot = "A1",
            Section = "1",
            FiscalYear = 2026,
            YearBuilt = 2005,
            Status = "active",
            MonthlyAssessment = 250m,
            AnnualAssessment = 3000m,
            AssessmentDueDay = 1,
            LateFeeAmount = 50m,
            LateFeeGraceDays = 15,
            FinanceChargeRate = 0.015m
        };

        var secondaryProperty = new Property
        {
            Id = Guid.NewGuid(),
            AccountNumber = "SAKURA-002",
            CommunityId = community.Id,
            Address = "2 Sakura Drive",
            City = "San Jose",
            State = "CA",
            Zip = "95101",
            Lot = "A2",
            Section = "1",
            FiscalYear = 2026,
            YearBuilt = 2005,
            Status = "active",
            MonthlyAssessment = 250m,
            AnnualAssessment = 3000m,
            AssessmentDueDay = 1,
            LateFeeAmount = 50m,
            LateFeeGraceDays = 15,
            FinanceChargeRate = 0.015m
        };

        // 016-A FR-A1a/A1b transition: an unclaimed property so the new claim-code registration flow
        // can be exercised. Existing user↔property links below remain valid (no re-claim needed).
        var unclaimedProperty = new Property
        {
            Id = Guid.NewGuid(),
            AccountNumber = "SAKURA-003",
            CommunityId = primaryProperty.CommunityId,
            Address = "3 Sakura Drive",
            City = primaryProperty.City,
            State = primaryProperty.State,
            Zip = primaryProperty.Zip,
            Status = primaryProperty.Status,
            MonthlyAssessment = primaryProperty.MonthlyAssessment,
            AnnualAssessment = primaryProperty.AnnualAssessment,
            AssessmentDueDay = primaryProperty.AssessmentDueDay,
            LateFeeAmount = primaryProperty.LateFeeAmount,
            LateFeeGraceDays = primaryProperty.LateFeeGraceDays,
            FinanceChargeRate = primaryProperty.FinanceChargeRate
        };

        db.Properties.AddRange(primaryProperty, secondaryProperty, unclaimedProperty);
        await db.SaveChangesAsync(ct);

        db.UserProperties.AddRange(
            new UserProperty { UserId = primaryUser.Id, PropertyId = primaryProperty.Id },
            new UserProperty { UserId = secondaryUser.Id, PropertyId = secondaryProperty.Id });
        await db.SaveChangesAsync(ct);

        var claimCodes = new Features.Auth.ClaimCodeService(
            db,
            services.GetRequiredService<Features.Auth.IAuthNotifier>(),
            services.GetRequiredService<ILogger<Features.Auth.ClaimCodeService>>());
        await claimCodes.IssueAsync(unclaimedProperty.Id, "owner-of-sakura-003@seed.local", ct);

        return new SeedResult(primaryUser.Id, secondaryUser.Id, primaryProperty.Id, secondaryProperty.Id, community.Id);
    }
}
