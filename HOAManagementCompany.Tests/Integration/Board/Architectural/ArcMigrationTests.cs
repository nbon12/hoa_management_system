using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>
/// T010 — the AddArchitecturalReview migration applied on the shared Testcontainers database:
/// tables exist, the one-vote-per-member index and the outbox single-recipient check are enforced,
/// and payment-style outbox rows (OwnerId only) are still valid.
/// </summary>
public class ArcMigrationTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    [Theory]
    [InlineData("CommunityArcSettings")]
    [InlineData("ArchitecturalApplications")]
    [InlineData("ArchitecturalAttachments")]
    [InlineData("ArchitecturalVotes")]
    [InlineData("ArchitecturalInfoRequests")]
    public async Task Migration_CreatesTable(string table)
    {
        var exists = await WithDbAsync(db => db.Database
            .SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = {table}) AS \"Value\"")
            .SingleAsync());
        Assert.True(exists);
    }

    [Fact]
    public async Task SecondVoteBySameMember_IsRejectedByUniqueIndex()
    {
        var s = await CreateScenarioAsync(3);
        var appId = await CreateApplicationAsync(s);
        await AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Approve));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddVotesAsync(appId, (s.Board[0], ArcVoteChoice.Deny)));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, (ex.InnerException as PostgresException)?.SqlState);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task OutboxRow_MustHaveExactlyOneRecipient(bool withOwner, bool withUser)
    {
        var s = await CreateScenarioAsync(1);
        var ownerId = await WithDbAsync(db => db.Owners.Where(o => o.PropertyId == s.PropertyId).Select(o => o.Id).FirstAsync());

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => WithDbAsync(async db =>
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                Kind = "arc_board_reminder",
                OwnerId = withOwner ? ownerId : null,
                RecipientUserId = withUser ? s.Board[0].UserId : null,
                PayloadJson = "{}"
            });
            return await db.SaveChangesAsync();
        }));
        Assert.Equal(PostgresErrorCodes.CheckViolation, (ex.InnerException as PostgresException)?.SqlState);
    }

    [Fact]
    public async Task OwnerOnlyOutboxRow_StillValid_AsPaymentAlertsWrite()
    {
        var s = await CreateScenarioAsync(1);
        var ownerId = await WithDbAsync(db => db.Owners.Where(o => o.PropertyId == s.PropertyId).Select(o => o.Id).FirstAsync());

        var saved = await WithDbAsync(async db =>
        {
            db.OutboxMessages.Add(new OutboxMessage { Kind = "email_alert", OwnerId = ownerId, PayloadJson = "{}" });
            return await db.SaveChangesAsync();
        });
        Assert.Equal(1, saved);
    }
}
