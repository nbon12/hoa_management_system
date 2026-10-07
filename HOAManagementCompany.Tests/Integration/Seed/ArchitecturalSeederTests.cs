using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Seed;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Seed;

/// <summary>
/// T022 — the 027 demo seeder: a five-member board plus a manager, the wireframe applications with
/// ARC-1042 awaiting board@nekohoa.dev's vote, a denied v1 with an open v2, and no duplicates on re-run.
/// </summary>
public class ArchitecturalSeederTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Seeder_CreatesDemoData_AndIsIdempotent()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DatabaseSeeder>>();

        // EnsureBoardUserAsync needs a claimed, non-registration property in the community:
        // arrange one, plus spare properties for the seeded applications.
        // The shared fixture seeds the community with a LegalName only (see SeederTests).
        var community = await db.Communities.FirstOrDefaultAsync(c => c.CommunityName == "Sakura Heights HOA");
        if (community is null)
        {
            community = new HOAManagementCompany.Domain.Entities.Community
            {
                Id = Guid.NewGuid(), CommunityName = "Sakura Heights HOA", LegalName = "Sakura Heights HOA",
                Status = CommunityStatus.Active
            };
            db.Communities.Add(community);
            await db.SaveChangesAsync();
        }
        var coResident = $"arc-seed-{Guid.NewGuid():N}";
        db.Users.Add(new HOAManagementCompany.Domain.Entities.ApplicationUser
        {
            Id = coResident, Email = $"{coResident}@example.com", UserName = $"{coResident}@example.com",
            FirstName = "Co", LastName = "Resident"
        });
        var claimed = await HOAManagementCompany.Tests.Integration.Board.Architectural.ArcTestBaseAccess.CreatePropertyAsync(db, community.Id);
        db.UserProperties.Add(new HOAManagementCompany.Domain.Entities.UserProperty
        {
            Id = Guid.NewGuid(), UserId = coResident, PropertyId = claimed
        });
        for (var i = 0; i < 4; i++)
            await HOAManagementCompany.Tests.Integration.Board.Architectural.ArcTestBaseAccess.CreatePropertyAsync(db, community.Id);
        await db.SaveChangesAsync();

        await new AuthSeeder(db, scope.ServiceProvider, logger).EnsureBoardUserAsync();
        Assert.True(await db.Users.AnyAsync(u => u.Email == "board@nekohoa.dev"));

        var seeder = new ArchitecturalSeeder(db, scope.ServiceProvider, logger);
        await seeder.SeedAsync();
        var appsAfterFirst = await db.ArchitecturalApplications.CountAsync(a => a.CommunityId == community.Id);
        var membershipsAfterFirst = await db.CommunityMemberships.CountAsync(m => m.CommunityId == community.Id);

        await seeder.SeedAsync();

        Assert.Equal(appsAfterFirst, await db.ArchitecturalApplications.CountAsync(a => a.CommunityId == community.Id));
        Assert.Equal(membershipsAfterFirst, await db.CommunityMemberships.CountAsync(m => m.CommunityId == community.Id));

        var boardCount = await db.CommunityMemberships.CountAsync(m =>
            m.CommunityId == community.Id && m.Role == CommunityRole.BoardMember && m.Status == MembershipStatus.Active);
        Assert.True(boardCount >= 5);
        Assert.True(await db.CommunityMemberships.AnyAsync(m =>
            m.CommunityId == community.Id && m.Role == CommunityRole.CommunityManager && m.User.Email == "manager@nekohoa.dev"));

        var fence = await db.ArchitecturalApplications.SingleAsync(a => a.CommunityId == community.Id && a.ApplicationNumber == 1042);
        Assert.Equal(ArcApplicationStatus.Open, fence.Status);
        Assert.Equal(3, await db.ArchitecturalAttachments.CountAsync(x => x.ApplicationId == fence.Id));
        var boardUserId = await db.Users.Where(u => u.Email == "board@nekohoa.dev").Select(u => u.Id).SingleAsync();
        Assert.False(await db.ArchitecturalVotes.AnyAsync(v => v.ApplicationId == fence.Id && v.VoterUserId == boardUserId));

        var paint = await db.ArchitecturalApplications
            .Where(a => a.CommunityId == community.Id && a.ApplicationNumber == 1039)
            .OrderBy(a => a.Revision).ToListAsync();
        Assert.Equal(2, paint.Count);
        Assert.Equal(ArcOutcome.Denied, paint[0].DecisionOutcome);
        Assert.Equal(ArcDenialWording.RevisionsRequested, paint[0].DecisionWording);
        Assert.Equal(paint[0].Id, paint[1].PreviousRevisionId);
        Assert.Equal(ArcApplicationStatus.Open, paint[1].Status);
    }
}
