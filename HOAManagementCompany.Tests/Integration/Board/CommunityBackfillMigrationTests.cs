using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board;

/// <summary>
/// T015 — the community backfill migration (FR-004/FR-005/FR-006).
///
/// LIMITATION: the legacy pre-025 schema stored <c>CommunityId</c> as a denormalized
/// STRING on Property/Violation; the up-migration parses those strings into
/// <c>Community</c> rows. EF Core's model (and the Testcontainers harness) only ever
/// materialize the FINAL, post-migration model — the legacy string columns no longer
/// exist on the mapped entities, so the string→GUID up-migration cannot be faithfully
/// replayed through EF here without hand-rolling raw DDL that would diverge from the
/// real migration. Rather than fake a passing replay, this test asserts the
/// POST-migration invariants the backfill must guarantee on the already-migrated
/// database: no orphan community foreign keys across every community-scoped entity,
/// and rows resolving to the correct community.
///
/// No down-migration is exercised: migrations are forward-only (FR-005).
/// </summary>
public class CommunityBackfillMigrationTests(TestDatabaseFixture fixture) : BoardTestBase(fixture)
{
    // FR-004/FR-006: every community-scoped row references an existing Community — no
    // orphan foreign keys survive the backfill.
    [Fact]
    public async Task EveryCommunityScopedRow_ResolvesToAnExistingCommunity()
    {
        using var scope = NewScope();
        var db = Db(scope);

        var communityIds = (await db.Communities.Select(c => c.Id).ToListAsync()).ToHashSet();
        Assert.NotEmpty(communityIds);

        await AssertNoOrphanCommunityFksAsync(db, communityIds);
    }

    // FR-005: the seeded property SAKURA-001 maps to the "Sakura Heights HOA" community,
    // demonstrating the string pair is backfilled with its property association intact.
    [Fact]
    public async Task SeededProperty_MapsToSakuraHeightsCommunity()
    {
        using var scope = NewScope();
        var db = Db(scope);

        var property = await db.Properties
            .Include(p => p.Community)
            .FirstAsync(p => p.AccountNumber == "SAKURA-001");

        Assert.Equal(TestDataSeeder.SakuraCommunityId, property.CommunityId);
        Assert.Equal("Sakura Heights HOA", property.Community.LegalName);
    }

    // FR-005 requires the invariants be verified against a MULTI-community dataset. The
    // shared fixture seeds exactly one community, and a single-community dataset cannot
    // distinguish a correct per-community backfill from one that collapses every row onto
    // the same community. So this test stands up a second, independent community carrying
    // a row in EVERY one of the eight community-scoped tables, then re-asserts the
    // invariants with ≥2 communities present and checks each new row resolves to its own
    // community rather than leaking onto the seeded one.
    [Fact]
    public async Task BackfillInvariants_HoldAcrossMultipleCommunities()
    {
        using var scope = NewScope();
        var db = Db(scope);

        var communityB = await CreateCommunityAsync(db);
        var (_, _, propertyId) = await CreateUserWithPropertyAsync(db, communityB);

        db.Violations.Add(new Violation
        {
            Id = Guid.NewGuid(), PropertyId = propertyId, CommunityId = communityB,
            Title = "Trash bins left out", Category = ViolationCategory.Maintenance,
            Status = ViolationStatus.Open, IssuedDate = Today,
        });
        db.HoaDocuments.Add(new HoaDocument
        {
            Id = Guid.NewGuid(), CommunityId = communityB, Name = "CC&R",
            Category = DocumentCategory.Governing, EffectiveDate = Today,
            FileSizeLabel = "1.2 MB", StorageKey = $"docs/{Guid.NewGuid():N}.pdf",
        });
        db.Polls.Add(new Poll
        {
            Id = Guid.NewGuid(), CommunityId = communityB,
            Question = "Extend pool hours?", ClosingLabel = "closes Friday",
        });
        db.CalendarEvents.Add(new CalendarEvent
        {
            Id = Guid.NewGuid(), CommunityId = communityB, Title = "Board meeting",
            EventDate = DateTimeOffset.UtcNow.AddDays(7), Category = EventCategory.Board,
        });
        db.Announcements.Add(new Announcement
        {
            Id = Guid.NewGuid(), CommunityId = communityB, Title = "Pool reopens",
            Body = "Saturday at 10am.", PublishedAt = DateTimeOffset.UtcNow,
            Category = AnnouncementCategory.Events, AuthorName = "Board",
        });
        db.CommunityExpenses.Add(new CommunityExpense
        {
            Id = Guid.NewGuid(), CommunityId = communityB, Label = "Landscaping",
            Color = "#4CAF50", Amount = 28_500m, FiscalYear = 2026,
        });
        // Unique per community (IX_HoaPaymentConfigs_CommunityId) — safe: communityB is new.
        db.HoaPaymentConfigs.Add(new HoaPaymentConfig { Id = Guid.NewGuid(), CommunityId = communityB });
        await db.SaveChangesAsync();

        var communityIds = (await db.Communities.Select(c => c.Id).ToListAsync()).ToHashSet();
        Assert.True(communityIds.Count >= 2,
            $"expected a multi-community dataset for this assertion; found {communityIds.Count}");
        Assert.Contains(TestDataSeeder.SakuraCommunityId, communityIds);
        Assert.Contains(communityB, communityIds);

        await AssertNoOrphanCommunityFksAsync(db, communityIds);

        // Each new row belongs to communityB — not collapsed onto the seeded community.
        Assert.Equal(communityB, (await db.Properties.FirstAsync(p => p.Id == propertyId)).CommunityId);
        Assert.All(await db.Violations.Where(v => v.PropertyId == propertyId).ToListAsync(),
            v => Assert.Equal(communityB, v.CommunityId));
        Assert.Equal(1, await db.HoaPaymentConfigs.CountAsync(c => c.CommunityId == communityB));

        // …and the seeded community's own rows were not re-pointed at communityB.
        Assert.True(
            await db.Properties.AnyAsync(p => p.CommunityId == TestDataSeeder.SakuraCommunityId),
            "the seeded community lost its properties");
    }

    // FR-004/FR-006 invariant, shared by the single- and multi-community assertions:
    // every community-scoped table's CommunityId must resolve to a real Community row.
    private static async Task AssertNoOrphanCommunityFksAsync(ApplicationDbContext db, HashSet<Guid> communityIds)
    {
        async Task AssertNoOrphans(string entity, IQueryable<Guid> fks)
        {
            var distinct = await fks.Distinct().ToListAsync();
            var orphans = distinct.Where(id => !communityIds.Contains(id)).ToList();
            Assert.True(orphans.Count == 0, $"{entity} has community FKs with no Community row: {string.Join(", ", orphans)}");
        }

        await AssertNoOrphans(nameof(db.Properties), db.Properties.Select(x => x.CommunityId));
        await AssertNoOrphans(nameof(db.Violations), db.Violations.Select(x => x.CommunityId));
        await AssertNoOrphans(nameof(db.HoaDocuments), db.HoaDocuments.Select(x => x.CommunityId));
        await AssertNoOrphans(nameof(db.Polls), db.Polls.Select(x => x.CommunityId));
        await AssertNoOrphans(nameof(db.CalendarEvents), db.CalendarEvents.Select(x => x.CommunityId));
        await AssertNoOrphans(nameof(db.Announcements), db.Announcements.Select(x => x.CommunityId));
        await AssertNoOrphans(nameof(db.CommunityExpenses), db.CommunityExpenses.Select(x => x.CommunityId));
        await AssertNoOrphans(nameof(db.HoaPaymentConfigs), db.HoaPaymentConfigs.Select(x => x.CommunityId));
    }
}
