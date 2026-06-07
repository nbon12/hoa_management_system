using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Alerts;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PaymentMethod = HOAManagementCompany.Domain.Enums.PaymentMethod;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// US3 opt-in matrix (FR-013, FR-015): <see cref="AlertService"/> enqueues exactly the channels the
/// owner opted in to — and never SMS without a phone on file. Alerts default OFF, so an owner who has
/// changed nothing receives nothing. Content is masked / PII-free (FR-018).
/// </summary>
public class AlertOptInTests(TestDatabaseFixture fixture) : AlertTestBase(fixture)
{
    private static readonly Guid PropertyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private async Task<PaymentTransaction> ArrangeAsync(
        ApplicationDbContext db, bool sms, bool email, string? phone)
    {
        await db.OutboxMessages.ExecuteDeleteAsync();   // isolate from prior tests in this class.
        var owner = await db.Owners.FirstAsync(o => o.PropertyId == PropertyId);
        owner.AlertSmsOptIn = sms;
        owner.AlertEmailOptIn = email;
        owner.AlertPhone = phone;

        var txn = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PropertyId = PropertyId,
            OwnerId = owner.Id,
            GrossAmount = 120m,
            Total = 123.45m,
            Status = TransactionStatus.Failed,
            PaymentMethod = PaymentMethod.Card,
            IsRecurring = true,
            FailureCode = "card_declined",
        };
        db.PaymentTransactions.Add(txn);
        await db.SaveChangesAsync();
        return txn;
    }

    [Theory]
    [InlineData(true, true, "+19195550123", 1, 1)]   // both channels
    [InlineData(true, false, "+19195550123", 1, 0)]  // sms only
    [InlineData(false, true, null, 0, 1)]            // email only (no phone needed)
    [InlineData(false, false, null, 0, 0)]           // fully opted out → nothing
    [InlineData(true, false, null, 0, 0)]            // sms opted in but NO phone → suppressed
    public async Task EnqueueFailureAlert_RespectsOptInMatrix(
        bool sms, bool email, string? phone, int expectSms, int expectEmail)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var alerts = scope.ServiceProvider.GetRequiredService<AlertService>();
        var txn = await ArrangeAsync(db, sms, email, phone);

        var enqueued = await alerts.EnqueueFailureAlertAsync(txn, txn.FailureCode);
        await db.SaveChangesAsync();

        var rows = await db.OutboxMessages.Where(m => m.TransactionId == txn.Id).ToListAsync();
        Assert.Equal(expectSms + expectEmail, enqueued);
        Assert.Equal(expectSms, rows.Count(r => r.Kind == "sms_alert"));
        Assert.Equal(expectEmail, rows.Count(r => r.Kind == "email_alert"));
        Assert.All(rows, r => Assert.Equal(OutboxStatus.Pending, r.Status));
    }

    [Fact]
    public async Task EnqueuedContent_IsMaskedAndPiiFree()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var alerts = scope.ServiceProvider.GetRequiredService<AlertService>();
        var txn = await ArrangeAsync(db, sms: true, email: true, phone: "+19195550123");

        await alerts.EnqueueFailureAlertAsync(txn, "insufficient_funds");
        await db.SaveChangesAsync();

        var rows = await db.OutboxMessages.Where(m => m.TransactionId == txn.Id).ToListAsync();
        Assert.Equal(2, rows.Count);
        foreach (var row in rows)
        {
            Assert.Contains("$123.45", row.PayloadJson);              // amount surfaced
            Assert.Contains("insufficient funds", row.PayloadJson);   // friendly reason
            Assert.DoesNotContain("insufficient_funds", row.PayloadJson); // raw code mapped, not leaked
        }
    }
}
