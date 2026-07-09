using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Factories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using HOAManagementCompany.Infrastructure.Payments;
using Npgsql;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Tests.Fixtures;

/// <summary>
/// Base for the payment-integrity (015 US1) tests: payment harness + a shared
/// <see cref="FaultInjectionInterceptor"/> wired into the DbContext options, and per-domain
/// factory seeding on an isolated property/community per test (parallel- and rerun-safe;
/// no coupling to the global <see cref="TestDataSeeder"/> magic ids).
/// </summary>
public abstract class FaultInjectedPaymentTestBase(TestDatabaseFixture fixture) : PaymentTestBase(fixture)
{
    /// <summary>Shared across all scopes of this test class, so an armed fault survives the
    /// scope boundary between the "crashing" delivery and its retry — like a real process.</summary>
    protected readonly FaultInjectionInterceptor Fault = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        // Re-register the DbContext with the fault interceptor appended, keeping the harness's
        // shared traced NpgsqlDataSource (see IntegrationTestBase) and warning suppression.
        var toRemove = services
            .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>))
            .ToList();
        foreach (var d in toRemove) services.Remove(d);
        services.AddDbContext<ApplicationDbContext>((sp, o) => o
            .UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>())
            .AddInterceptors(Fault)
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
    }

    /// <summary>
    /// Seeds an isolated property (unique community) with an owner and an NSF-enabled payment
    /// config, plus one transaction in the given state. Everything is unique per call, so tests
    /// never contend on the shared seed data.
    /// </summary>
    protected async Task<PaymentTransaction> SeedIsolatedTransactionAsync(
        TransactionStatus status, PaymentMethod method, decimal gross = 250m)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var property = PropertyFactory.Create(
            communityId: $"US1-{suffix}", accountNumber: $"US1-{suffix}");
        var owner = OwnerFactory.Create(property.Id, email: $"us1-{suffix}@test.dev");
        db.Properties.Add(property);
        db.Owners.Add(owner);
        db.HoaPaymentConfigs.Add(PaymentFactory.NsfEnabledConfig(property.CommunityId));

        var txn = PaymentFactory.Transaction(property.Id, owner.Id, status, method, gross);
        db.PaymentTransactions.Add(txn);
        await db.SaveChangesAsync();
        return txn;
    }

    // ── Neutral provider-event builders (015 FR-021: handlers are testable without SDK types) ──

    protected static PaymentProviderEvent SucceededEvent(string intentId, string chargeId) => new(
        $"evt_{Guid.NewGuid():N}", PaymentProviderEventKind.PaymentSucceeded, "payment_intent.succeeded",
        PaymentIntentId: intentId, LatestChargeId: chargeId);

    protected static PaymentProviderEvent AchReturnEvent(string intentId) => new(
        $"evt_{Guid.NewGuid():N}", PaymentProviderEventKind.PaymentFailed, "payment_intent.payment_failed",
        PaymentIntentId: intentId, FailureCode: "R01", FailureMessage: "insufficient funds");

    protected static PaymentProviderEvent RefundEvent(string chargeId, long amountRefundedCents) => new(
        $"evt_{Guid.NewGuid():N}", PaymentProviderEventKind.Refunded, "charge.refunded",
        ChargeId: chargeId, AmountRefunded: amountRefundedCents / 100m);

    protected static PaymentProviderEvent DisputeCreatedEvent(string chargeId) => new(
        $"evt_{Guid.NewGuid():N}", PaymentProviderEventKind.DisputeCreated, "charge.dispute.created",
        ChargeId: chargeId, DisputeId: $"dp_{Guid.NewGuid():N}", DisputeStatus: "warning_needs_response");

    protected static PaymentProviderEvent DisputeClosedEvent(string chargeId, string status) => new(
        $"evt_{Guid.NewGuid():N}", PaymentProviderEventKind.DisputeClosed, "charge.dispute.closed",
        ChargeId: chargeId, DisputeId: $"dp_{Guid.NewGuid():N}", DisputeStatus: status);

    /// <summary>Runs one webhook delivery in its own DI scope — a fresh DbContext, exactly like
    /// a real delivery or reconcile retry. Returns the thrown exception, if any.</summary>
    protected async Task<Exception?> DeliverAsync(PaymentProviderEvent evt)
    {
        using var scope = Services.CreateScope();
        var processor = scope.ServiceProvider
            .GetRequiredService<HOAManagementCompany.Features.Payments.Webhooks.WebhookProcessor>();
        try
        {
            await processor.ProcessAsync(evt);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>Fresh-scope ledger/status snapshot for assertions.</summary>
    protected async Task<(PaymentTransaction Txn, List<LedgerEntry> Entries, int Receipts)> SnapshotAsync(Guid txnId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var txn = await db.PaymentTransactions.AsNoTracking().FirstAsync(t => t.Id == txnId);
        var entries = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.TransactionId == txnId).OrderBy(e => e.Sequence).ToListAsync();
        var receipts = await db.Receipts.AsNoTracking().CountAsync(r => r.TransactionId == txnId);
        return (txn, entries, receipts);
    }
}
