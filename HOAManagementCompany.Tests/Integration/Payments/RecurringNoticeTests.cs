using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Payments;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// US2 recurring guarantees that ride the run-drafts sweep: NACHA variable-amount advance notice
/// for open-balance auto-pay (FR-011c), a disabled mandate producing no further drafts (FR-011),
/// and a failed draft that is NOT auto-retried within the cycle but reattempts on the next draft
/// day (FR-011a). Stripe and the alert providers are in-memory fakes; no network calls in CI.
/// </summary>
public class RecurringNoticeTests(TestDatabaseFixture fixture) : AlertTestBase(fixture)
{
    private const string SchedulerSecret = "test-scheduler-secret";
    private static readonly Guid PrimaryPropertyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

    private HttpRequestMessage RunDrafts(string date, string? secret = SchedulerSecret)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/payments/jobs/run-drafts?date={date}");
        if (secret is not null) req.Headers.Add("X-Scheduler-Secret", secret);
        return req;
    }

    private async Task<HttpResponseMessage> UpsertAsync(string setupIntentId, object body)
    {
        Stripe.SetVaultedMethod(setupIntentId, new StripeVaultedMethod(
            $"pm_{setupIntentId}", "mandate_test", "card", CardFunding.Credit, "visa", "4242"));
        return await Client.PutAsJsonAsync("/api/v1/payments/recurring", body);
    }

    /// <summary>
    /// Seeds an isolated property + owner + active open-balance (variable) mandate with a known
    /// positive ledger balance, so the notice sweep is deterministic regardless of other tests'
    /// activity on the shared collection database. Returns the owner id.
    /// </summary>
    private async Task<Guid> SeedBalanceMandateAsync(Guid propertyId, int draftDay, decimal balance, bool smsOptIn)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!await db.Properties.AnyAsync(p => p.Id == propertyId))
            db.Properties.Add(new HOAManagementCompany.Domain.Entities.Property
            {
                Id = propertyId,
                AccountNumber = $"SAKURA-{propertyId.ToString("N")[^6..]}",
                CommunityId = "SAKURA",
                CommunityName = "Sakura Heights HOA",
                Address = "56 Sakura Drive",
                City = "San Jose", State = "CA", Zip = "95101", Lot = "A56", Section = "1",
                FiscalYear = 2026, YearBuilt = 2005, Status = "active",
                MonthlyAssessment = 250m, AnnualAssessment = 3000m,
                AssessmentDueDay = 1, LateFeeAmount = 50m, LateFeeGraceDays = 15, FinanceChargeRate = 0.015m,
            });

        var owner = await db.Owners.FirstOrDefaultAsync(o => o.PropertyId == propertyId);
        if (owner is null)
        {
            owner = new Owner
            {
                Id = Guid.NewGuid(), PropertyId = propertyId,
                FirstName = "Vera", LastName = "Variable",
                Email = "variable@nekohoa.dev", Phone = "408-555-0156",
                MailingToProperty = true, VotingRights = true,
            };
            db.Owners.Add(owner);
        }
        owner.StripeCustomerId = "cus_notice_test";
        owner.AlertSmsOptIn = smsOptIn;
        owner.AlertPhone = smsOptIn ? "+19195550156" : null;

        // Fresh mandate + ledger each call so the period/dedup keys are deterministic.
        await db.RecurringPayments.Where(r => r.PropertyId == propertyId).ExecuteDeleteAsync();
        await db.LedgerEntries.Where(e => e.PropertyId == propertyId).ExecuteDeleteAsync();
        await db.OutboxMessages.Where(m => m.OwnerId == owner.Id).ExecuteDeleteAsync();

        db.RecurringPayments.Add(new RecurringPayment
        {
            Id = Guid.NewGuid(), PropertyId = propertyId,
            AmountType = RecurringAmountType.Balance, Method = PaymentMethod.Card,
            DraftDay = draftDay, Status = "active", VaultedPaymentMethodId = "pm_notice_test",
            MethodBrand = "visa", MethodLast4 = "4242", MethodFunding = CardFunding.Credit,
        });
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(), PropertyId = propertyId, Sequence = 1,
            EntryDate = new DateOnly(2026, 1, 1), Description = "Outstanding balance",
            ChargeAmount = balance, RunningBalance = balance, EntryType = LedgerEntryType.RegularAssessment,
        });
        await db.SaveChangesAsync();
        return owner.Id;
    }

    [Fact]
    public async Task VariableNotice_EnqueuedLeadDaysBeforeDraft_AndDispatches()
    {
        // Open-balance mandate drafting on day 15; the 10-day NACHA lead puts the notice on day 5.
        var propertyId = Guid.Parse("cccccccc-0000-0000-0000-000000000015");
        var ownerId = await SeedBalanceMandateAsync(propertyId, draftDay: 15, balance: 300m, smsOptIn: true);

        var run = await JsonAsync(await Client.SendAsync(RunDrafts("2026-08-05")));
        Assert.True(run.GetProperty("noticesSent").GetInt32() >= 1);
        // Day 5 is not the draft day, so nothing is charged.
        Assert.Equal(0, run.GetProperty("charged").GetInt32());

        // Both channels (email always; SMS because opted in) are enqueued with a dedup key, Pending.
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
            var notices = await db.OutboxMessages
                .Where(m => m.OwnerId == ownerId && m.Kind.StartsWith("variable_notice"))
                .ToListAsync();
            Assert.Equal(2, notices.Count);
            Assert.All(notices, n => Assert.False(string.IsNullOrEmpty(n.DedupKey)));
            Assert.All(notices, n => Assert.Equal(OutboxStatus.Pending, n.Status));

            await dispatcher.DispatchPendingAsync();
        }

        Assert.Contains(Email.Sent, m => m.Subject == "NekoHOA: upcoming automatic payment");
        Assert.Contains(Sms.Sent, m => m.Body.Contains("auto-pay of"));

        // Idempotent: a same-day re-run enqueues nothing new (dedup key already present).
        var rerun = await JsonAsync(await Client.SendAsync(RunDrafts("2026-08-05")));
        Assert.Equal(0, rerun.GetProperty("noticesSent").GetInt32());
    }

    [Fact]
    public async Task VariableNotice_PaidUpBalance_SendsNoNotice()
    {
        // Zero balance ⇒ nothing will be drafted ⇒ no NACHA notice is required.
        var propertyId = Guid.Parse("cccccccc-0000-0000-0000-000000000016");
        var ownerId = await SeedBalanceMandateAsync(propertyId, draftDay: 16, balance: 0m, smsOptIn: false);

        await Client.SendAsync(RunDrafts("2026-08-06")); // 10 days before day 16.

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notices = await db.OutboxMessages
            .Where(m => m.OwnerId == ownerId && m.Kind.StartsWith("variable_notice"))
            .CountAsync();
        Assert.Equal(0, notices);
    }

    [Fact]
    public async Task DisabledMandate_ProducesNoDrafts()
    {
        await AuthenticateAsync();
        await UpsertAsync("seti_disable", new
        {
            amountType = "fixed", fixedAmount = 40m, draftDay = 10,
            setupIntentId = "seti_disable", mandateAccepted = true,
        });

        // Disable auto-pay (FR-011): the schedule must produce no further drafts.
        var del = await Client.DeleteAsync("/api/v1/payments/recurring");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, del.StatusCode);

        var run = await JsonAsync(await Client.SendAsync(RunDrafts("2026-10-10")));
        Assert.Equal(0, run.GetProperty("dueCount").GetInt32());
        Assert.Equal(0, run.GetProperty("charged").GetInt32());
        Assert.DoesNotContain(Stripe.OffSessionCharges, c => c.PaymentMethodId == "pm_seti_disable");
    }

    [Fact]
    public async Task FailedDraft_NotRetriedWithinCycle_RetriesNextDraftDay()
    {
        await AuthenticateAsync();
        await UpsertAsync("seti_retry", new
        {
            amountType = "fixed", fixedAmount = 60m, draftDay = 10,
            setupIntentId = "seti_retry", mandateAccepted = true,
        });

        // Force the vaulted method to decline off-session.
        Stripe.SetOffSessionOutcome("pm_seti_retry", new StripePaymentIntentResult(
            "pi_retry_fail", "pi_retry_fail_secret", "requires_payment_method", 0, "usd", "card", null,
            CardFunding.Credit, "visa", "4242", FailureCode: "card_declined", FailureMessage: "Your card was declined."));

        // Draft day for October: one declined attempt is recorded.
        var first = await JsonAsync(await Client.SendAsync(RunDrafts("2026-10-10")));
        Assert.Equal(1, first.GetProperty("failed").GetInt32());
        Assert.Equal(1, Stripe.OffSessionCharges.Count(c => c.PaymentMethodId == "pm_seti_retry"));

        // FR-011a: a re-run within the same cycle MUST NOT retry — it is skipped, no new charge.
        var sameCycle = await JsonAsync(await Client.SendAsync(RunDrafts("2026-10-10")));
        Assert.Equal(0, sameCycle.GetProperty("charged").GetInt32());
        Assert.Equal(0, sameCycle.GetProperty("failed").GetInt32());
        Assert.Equal(1, sameCycle.GetProperty("skipped").GetInt32());
        Assert.Equal(1, Stripe.OffSessionCharges.Count(c => c.PaymentMethodId == "pm_seti_retry"));

        // A real reattempt is a fresh PaymentIntent — give the decline a new id so it doesn't collide
        // with last cycle's transaction on the unique StripePaymentIntentId index.
        Stripe.SetOffSessionOutcome("pm_seti_retry", new StripePaymentIntentResult(
            "pi_retry_fail_nov", "pi_retry_fail_nov_secret", "requires_payment_method", 0, "usd", "card", null,
            CardFunding.Credit, "visa", "4242", FailureCode: "card_declined", FailureMessage: "Your card was declined."));

        // Next scheduled draft day (next month) reattempts — proving it waited for the next cycle.
        var nextCycle = await JsonAsync(await Client.SendAsync(RunDrafts("2026-11-10")));
        Assert.Equal(1, nextCycle.GetProperty("failed").GetInt32());
        Assert.Equal(2, Stripe.OffSessionCharges.Count(c => c.PaymentMethodId == "pm_seti_retry"));
    }
}
