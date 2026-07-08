using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Factories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Stripe;
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
        db.HoaPaymentConfigs.Add(new HoaPaymentConfig
        {
            Id = Guid.NewGuid(),
            CommunityId = property.CommunityId,
            NsfFeeEnabled = true,
            NsfFeeAmount = 25m,
        });

        var txn = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            OwnerId = owner.Id,
            StripePaymentIntentId = $"pi_test_{Guid.NewGuid():N}",
            StripeChargeId = $"ch_test_{Guid.NewGuid():N}",
            GrossAmount = gross,
            FeeAmount = 0m,
            Total = gross,
            Status = status,
            PaymentMethod = method,
        };
        db.PaymentTransactions.Add(txn);
        await db.SaveChangesAsync();
        return txn;
    }

    // ── Stripe event builders (mirrors WebhookProcessorTests conventions) ─────────────────

    protected static Event Evt(string type, object dataObject) => EventUtility.ParseEvent(
        JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}",
            @object = "event",
            type,
            api_version = StripeConfiguration.ApiVersion,
            request = (string?)null,
            data = new { @object = dataObject },
        }));

    protected static Event SucceededEvent(string intentId, string chargeId) => Evt(
        "payment_intent.succeeded",
        new { id = intentId, @object = "payment_intent", latest_charge = chargeId });

    protected static Event AchReturnEvent(string intentId) => Evt(
        "payment_intent.payment_failed",
        new { id = intentId, @object = "payment_intent", last_payment_error = new { code = "R01", message = "insufficient funds" } });

    protected static Event RefundEvent(string chargeId, long amountRefundedCents) => Evt(
        "charge.refunded",
        new { id = chargeId, @object = "charge", amount_refunded = amountRefundedCents });

    protected static Event DisputeCreatedEvent(string chargeId) => Evt(
        "charge.dispute.created",
        new { id = $"dp_{Guid.NewGuid():N}", @object = "dispute", charge = chargeId, status = "warning_needs_response" });

    protected static Event DisputeClosedEvent(string chargeId, string status) => Evt(
        "charge.dispute.closed",
        new { id = $"dp_{Guid.NewGuid():N}", @object = "dispute", charge = chargeId, status });

    /// <summary>Runs one webhook delivery in its own DI scope — a fresh DbContext, exactly like
    /// a real delivery or reconcile retry. Returns the thrown exception, if any.</summary>
    protected async Task<Exception?> DeliverAsync(Event evt)
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
