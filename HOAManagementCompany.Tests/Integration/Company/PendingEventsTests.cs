using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Serilog.Events;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 FR-040, R5: a change event written inside a community or company lock is written once, after
/// the lock's transaction commits. The lock body runs in a retrying execution strategy, so a retried
/// attempt must not write its events twice, and an attempt that rolls back must write none.
/// </summary>
public class PendingEventsTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [Fact]
    public async Task AChangeInsideTheLock_IsWrittenOnce_AfterTheCommit()
    {
        var (community, senderId, invitationId) = await OrphanedInvitationAsync();

        var attempts = await RevokeInsideTheLockAsync(community, senderId, _ =>
        {
            Assert.Empty(Events(invitationId));
            return Task.CompletedTask;
        });

        Assert.Equal(1, attempts);
        AssertRevokedOnce(invitationId);
        Assert.Equal(InvitationState.Revoked, await StateAsync(invitationId));
    }

    [Fact]
    public async Task AChangeInsideALockThatRollsBack_IsNeverWritten()
    {
        var (community, senderId, invitationId) = await OrphanedInvitationAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RevokeInsideTheLockAsync(community, senderId, _ => throw new InvalidOperationException("refused")));

        Assert.Empty(Events(invitationId));
        Assert.Equal(InvitationState.Pending, await StateAsync(invitationId));
    }

    // The commit lands, but its acknowledgement is lost and the strategy retries. The retry finds
    // nothing left to revoke, so the committed change's event must come from the first attempt.
    [Fact]
    public async Task ACommitWhoseOutcomeIsUnknown_StillWritesItsChange()
    {
        var (community, senderId, invitationId) = await OrphanedInvitationAsync();
        var lostAck = new LoseTheFirstCommitAcknowledgement();

        var attempts = await RevokeInsideTheLockAsync(community, senderId, _ => Task.CompletedTask, lostAck);

        Assert.Equal((2, true), (attempts, lostAck.Fired));
        AssertRevokedOnce(invitationId);
        Assert.Equal(InvitationState.Revoked, await StateAsync(invitationId));
    }

    [Fact]
    public async Task ARetriedAttempt_WritesItsChangeOnce()
    {
        var (community, senderId, invitationId) = await OrphanedInvitationAsync();

        var attempts = await RevokeInsideTheLockAsync(community, senderId, attempt => attempt == 1
            ? throw new PostgresException("could not serialize access", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure)
            : Task.CompletedTask);

        Assert.Equal(2, attempts);
        AssertRevokedOnce(invitationId);
        Assert.Equal(InvitationState.Revoked, await StateAsync(invitationId));
    }

    /// <summary>A pending residency invitation whose sender has just stopped being a manager.</summary>
    private async Task<(Guid Community, string SenderId, Guid InvitationId)> OrphanedInvitationAsync()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (senderId, _) = await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community);
        var (invitationId, _) = await InsertResidencyInvitationAsync(companyId, community, home,
            $"kim-{Guid.NewGuid():N}@example.com", senderId, InvitationState.Pending);
        await WithDbAsync(db => db.CommunityMemberships.Where(m => m.UserId == senderId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, MembershipStatus.Inactive)));
        return (community, senderId, invitationId);
    }

    /// <summary>
    /// Revokes the sender's orphaned invitations inside the community lock, as ending a manager does
    /// (the body doesn't reset the tracker itself), on a context that retries like the app's.
    /// <paramref name="afterRevoke"/> runs before the commit.
    /// </summary>
    private async Task<int> RevokeInsideTheLockAsync(Guid community, string senderId, Func<int, Task> afterRevoke,
        IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DatabaseConnectionString(), o => o.EnableRetryOnFailure(3))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        if (interceptor is not null)
            options.AddInterceptors(interceptor);
        await using var db = new ApplicationDbContext(options.Options);
        var authority = new SenderAuthority(db, new CommunityScopeResolver(db), Clock,
            Services.GetRequiredService<ILogger<SenderAuthority>>());
        var attempts = 0;
        await CommunityLocks.RunAsync(db, community, async () =>
        {
            attempts++;
            var revoked = await authority.RevokeOrphanedAsync(senderId, community, "actor-1", "test", default);
            await db.SaveChangesAsync();
            await afterRevoke(attempts);
            return revoked;
        }, default);
        return attempts;
    }

    private void AssertRevokedOnce(Guid invitationId)
    {
        var e = Assert.Single(Events(invitationId));
        Assert.Equal(("Revoked", "SenderLostAuthority", "actor-1"), (Prop(e, "Change"), Prop(e, "Reason"), Prop(e, "ActorId")));
    }

    private List<LogEvent> Events(Guid invitationId) => LogSink.Events
        .Where(e => e.MessageTemplate.Text.StartsWith("InvitationChange") && Prop(e, "InvitationId") == invitationId.ToString())
        .ToList();

    private Task<InvitationState> StateAsync(Guid invitationId) =>
        WithDbAsync(db => db.Invitations.AsNoTracking().Where(i => i.Id == invitationId).Select(i => i.State).SingleAsync());

    private static string? Prop(LogEvent e, string name) =>
        e.Properties.TryGetValue(name, out var v) ? v.ToString().Trim('"') : null;

    /// <summary>Lets the first commit reach the database, then reports it as a lost connection.</summary>
    private sealed class LoseTheFirstCommitAcknowledgement : DbTransactionInterceptor
    {
        public bool Fired { get; private set; }

        public override Task TransactionCommittedAsync(System.Data.Common.DbTransaction transaction,
            TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (Fired)
                return Task.CompletedTask;
            Fired = true;
            throw new PostgresException("connection lost", "FATAL", "FATAL", PostgresErrorCodes.ConnectionFailure);
        }
    }
}
