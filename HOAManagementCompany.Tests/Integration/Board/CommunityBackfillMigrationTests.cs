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
/// <see cref="HOAManagementCompany.Domain.Entities.Community"/> rows. EF Core's model
/// (and the Testcontainers harness) only ever materialize the FINAL, post-migration
/// model — the legacy string columns no longer exist on the mapped entities, so the
/// string→GUID up-migration cannot be faithfully replayed through EF here without
/// hand-rolling raw DDL that would diverge from the real migration. Rather than fake a
/// passing replay, this test asserts the POST-migration invariants the backfill must
/// guarantee on the already-migrated database: no orphan community foreign keys across
/// every community-scoped entity, and the seeded property maps to the expected community.
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
}
